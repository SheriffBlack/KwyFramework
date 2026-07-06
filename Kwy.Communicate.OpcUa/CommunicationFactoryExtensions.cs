using Kwy.Communicate.Abstractions;
using Opc.Ua.Client;

namespace Kwy.Communicate.OpcUa;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactory RegisterOpcUa(this ICommunicationFactory factory, ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        factory.RegisterCreator<OpcUaConfig>(config => new OpcUaCommunication(config, sessionFactory));
        return factory;
    }
}
