﻿using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Respite.Bedrock.Internal;
using System.Threading;
using System.Threading.Tasks;

namespace Respite.Bedrock
{

    public sealed class RespBedrockProtocol : RespConnection
    {
        // private readonly ConnectionContext _connection;
        private readonly ProtocolReader _reader;
        private readonly ProtocolWriter _writer;

        public RespBedrockProtocol(ConnectionContext connection)
        {
            // _connection = connection;
            _reader = connection.CreateReader();
            _writer = connection.CreateWriter();
        }

        public override void Send(in RespValue value)
        {
            var vt = SendAsync(value, default);
            if (!vt.IsCompletedSuccessfully) vt.AsTask().Wait();
        }

        public override Lifetime<RespValue> Receive()
        {
            var vt = ReceiveAsync(default);
            return vt.IsCompletedSuccessfully ? vt.Result : vt.AsTask().Result;
        }

        public override ValueTask SendAsync(RespValue frame, CancellationToken cancellationToken)
            => _writer.WriteAsync<RespValue>(RespFormatter.Instance, frame, cancellationToken);

        public override ValueTask<Lifetime<RespValue>> ReceiveAsync(CancellationToken cancellationToken)
        {
            var result = _reader.ReadAsync<RespValue>(RespFormatter.Instance, cancellationToken);
            // avoid the async machinery if we already have the result on the pipe
            return result.IsCompletedSuccessfully ? new ValueTask<Lifetime<RespValue>>(Validate(_reader, result.Result)) : Awaited(_reader, result);

            static async ValueTask<Lifetime<RespValue>> Awaited(ProtocolReader reader, ValueTask<ProtocolReadResult<RespValue>> result)
                => Validate(reader, await result.ConfigureAwait(false));

            static Lifetime<RespValue> Validate(ProtocolReader reader, in ProtocolReadResult<RespValue> result)
            {
                reader.Advance();
                if (result.IsCanceled) ThrowCanceled();
                if (result.IsCompleted) ThrowAborted();
                return new Lifetime<RespValue>(result.Message, (_, state) => ((ProtocolReader)state).Advance(), reader);
            }
        }
    }
}
