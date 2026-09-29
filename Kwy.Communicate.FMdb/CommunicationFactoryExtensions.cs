using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// Registration helpers for the common communication factory.
/// </summary>
public static class CommunicationFactoryExtensions
{
    /// <summary>
    /// Registers the FluentModbus communication creator in the communication factory.
    /// </summary>
    public static ICommunicationFactoryRegistry RegisterFluentModbus(this ICommunicationFactoryRegistry factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<MdbConfig>(config => new FMdbCommunication(config));
        return factory;
    }
}
