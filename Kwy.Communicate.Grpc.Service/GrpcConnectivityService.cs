using Grpc.Core;
using Kwy.Communicate.Grpc.Contracts.V1;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// Baseline connectivity endpoint implemented by every Kwy gRPC host.
/// </summary>
public sealed class GrpcConnectivityService : ConnectivityService.ConnectivityServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
        => Task.FromResult(new PingReply
        {
            ServiceName = context.Host,
            ServerTimeUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
}
