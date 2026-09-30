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
    public static CommunicationFactoryBuilder RegisterFluentModbus(this CommunicationFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<MdbConfig>(config => new FMdbCommunication(config));
    }
}
