using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Opc.Ua.Client;

namespace Kwy.Communicate.OpcUa;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactoryRegistry RegisterOpcUa(this ICommunicationFactoryRegistry factory, ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        factory.RegisterCreator<OpcUaConfig>(config => new OpcUaCommunication(config, sessionFactory));
        return factory;
    }
}
