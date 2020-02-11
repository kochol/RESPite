﻿using Bedrock.Framework;
using BedrockRespProtocol;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Resp;
using Resp.Redis;
using StackExchange.Redis;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RunProfiler
{
    public static class Program
    {
        public static void Main() => BenchmarkRunner.Run<RedisPingPong>();
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RedisPingPong : IAsyncDisposable
{
    private ConnectionMultiplexer _muxer;
    private IServer _server;
    private RespConnection _bedrock, _directSocket;
    private Socket _socket;
    private ConnectionContext _connection;

    [BenchmarkCategory("Async")]
    [Benchmark(Baseline = true)]
    public Task SERedisAsync() => _server.PingAsync();

    [BenchmarkCategory("Async")]
    [Benchmark]
    public Task BedrockAsync() => _bedrock.PingAsync().AsTask();

    [BenchmarkCategory("Async")]
    [Benchmark]
    public Task DirectSocketAsync() => _directSocket.PingAsync().AsTask();

    [BenchmarkCategory("Sync")]
    [Benchmark(Baseline = true)]
    public void SERedis() => _server.Ping();

    [BenchmarkCategory("Sync")]
    [Benchmark]
    public void Bedrock() => _bedrock.Ping();

    [BenchmarkCategory("Sync")]
    [Benchmark]
    public void DirectSocket() => _directSocket.Ping();


    public ValueTask DisposeAsync()
    {
        _muxer?.Dispose();
        _socket?.Dispose();
        return _connection == null ? default : _connection.DisposeAsync();
    }

    [GlobalSetup]
    public async Task ConnectAsync()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 6379);
        _muxer = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { endpoint }
        });
        _server = _muxer.GetServer(endpoint);
        await SERedisAsync();

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var client = new ClientBuilder(serviceProvider)
            .UseSockets()
            .Build();

        _connection = await client.ConnectAsync(endpoint);
        _bedrock = new RespBedrockProtocol(_connection);
        await BedrockAsync();

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.Connect(endpoint);
        _directSocket = RespConnection.Create(_socket);
        await DirectSocketAsync();
    }
}