using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// Registration helpers for the common communication factory.
/// </summary>
public static class ProtocolClientFactoryExtensions
{
    /// <summary>
    /// Registers the FluentModbus communication creator in the communication factory.
    /// </summary>
    public static ProtocolClientFactoryBuilder RegisterFluentModbus(this ProtocolClientFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<MdbConfig>(config => new FMdbCommunication(config));
    }
}
