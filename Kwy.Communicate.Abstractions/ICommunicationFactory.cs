namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Optionally creates communication clients from protocol configurations when the concrete
/// protocol is selected at runtime. Applications with a known protocol should construct the
/// concrete client directly.
/// </summary>
public interface ICommunicationFactory
{
    ICommunicationClient CreateClient(IProtocolConfig config);

    TCommunication Create<TCommunication, TConfig>(TConfig config)
        where TCommunication : class, ICommunicationClient
        where TConfig : IProtocolConfig;
}

/// <summary>
/// Creates one communication client for a supported protocol configuration type.
/// Implementations should be stateless or thread-safe because they are commonly shared.
/// </summary>
public interface ICommunicationClientCreator
{
    Type ConfigType { get; }

    ICommunicationClient Create(IProtocolConfig config);
}

/// <summary>Strongly typed creator contract for one protocol configuration type.</summary>
public interface ICommunicationClientCreator<in TConfig> : ICommunicationClientCreator
    where TConfig : class, IProtocolConfig
{
    ICommunicationClient Create(TConfig config);

    Type ICommunicationClientCreator.ConfigType => typeof(TConfig);

    ICommunicationClient ICommunicationClientCreator.Create(IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Create(config as TConfig
            ?? throw new ArgumentException(
                $"Expected configuration type '{typeof(TConfig).FullName}', got '{config.GetType().FullName}'.",
                nameof(config)));
    }
}
