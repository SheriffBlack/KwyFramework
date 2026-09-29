using Grpc.Core;
using Kwy.Communicate.Grpc.Contracts.V1;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// 每个 Kwy gRPC Host 都应实现的基础连通性端点。
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
