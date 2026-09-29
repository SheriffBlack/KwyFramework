using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.NI;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactoryRegistry RegisterGpib(this ICommunicationFactoryRegistry factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<GpibConfig>(config => new GpibCommunication(config));
        return factory;
    }
}
