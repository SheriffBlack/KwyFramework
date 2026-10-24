using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.Grpc.Client.Configs;

namespace Kwy.Communicate.Grpc.Client;

/// <summary>
/// Kwy gRPC 通信客户端的工厂注册扩展。
/// </summary>
public static class ProtocolClientFactoryExtensions
{
    public static ProtocolClientFactoryBuilder RegisterGrpcClients(this ProtocolClientFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<GrpcConfig>(config => new GrpcCommunication(config));
    }
}
