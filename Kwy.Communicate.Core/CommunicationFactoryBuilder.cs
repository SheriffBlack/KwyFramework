using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

/// <summary>
/// Composes only the protocol creators required by a configuration-driven host and produces an
/// immutable factory. This builder is independent of dependency injection and creates no clients.
/// </summary>
public sealed class CommunicationFactoryBuilder
{
    private readonly Dictionary<Type, ICommunicationClientCreator> creators = new();
    private bool built;

    public CommunicationFactoryBuilder AddCreator(ICommunicationClientCreator creator)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(creator);

        if (!creators.TryAdd(creator.ConfigType, creator))
        {
            throw new InvalidOperationException(
                $"A communication creator for configuration type '{creator.ConfigType.FullName}' is already registered.");
        }

        return this;
    }

    /// <summary>Registers a delegate-based creator for one protocol configuration type.</summary>
    public CommunicationFactoryBuilder RegisterCreator<TConfig>(
        Func<TConfig, ICommunicationClient> creator)
        where TConfig : class, IProtocolConfig
    {
        ArgumentNullException.ThrowIfNull(creator);
        return AddCreator(new DelegateCommunicationClientCreator<TConfig>(creator));
    }

    public CommunicationFactory Build()
    {
        ThrowIfBuilt();
        built = true;
        return new CommunicationFactory(creators.Values);
    }

    private void ThrowIfBuilt()
    {
        if (built)
        {
            throw new InvalidOperationException("The communication factory builder has already been built and is immutable.");
        }
    }

    private sealed class DelegateCommunicationClientCreator<TConfig>(
        Func<TConfig, ICommunicationClient> creator) : ICommunicationClientCreator<TConfig>
        where TConfig : class, IProtocolConfig
    {
        public ICommunicationClient Create(TConfig config) => creator(config);
    }
}
