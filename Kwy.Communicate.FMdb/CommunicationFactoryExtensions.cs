using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// Registration helpers for the common communication factory.
/// </summary>
public static class CommunicationFactoryExtensions
{
    /// <summary>
    /// Registers the FluentModbus communication creator in the communication factory.
    /// </summary>
    public static ICommunicationFactory RegisterFluentModbus(this ICommunicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<MdbConfig>(config => new FMdbCommunication(config));
        return factory;
    }
}
