using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Grpc.Client.Configs;

namespace Kwy.Communicate.Grpc.Client;

/// <summary>
/// Factory registration for Kwy gRPC communication clients.
/// </summary>
public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactory RegisterGrpcClients(this ICommunicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<GrpcConfig>(config => new GrpcCommunication(config));
        return factory;
    }
}
