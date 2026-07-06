using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.NI;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactory RegisterGpib(this ICommunicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<GpibConfig>(config => new GpibCommunication(config));
        return factory;
    }
}
