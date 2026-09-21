using Grpc.Core;
using Kwy.Communicate.Grpc.Contracts.V1;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// 每个 Kwy gRPC 主机实现的基线连接端点
/// </summary>
public sealed class KwyGrpcConnectivityService : ConnectivityService.ConnectivityServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
        => Task.FromResult(new PingReply
        {
            ServiceName = context.Host,
            ServerTimeUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
}
