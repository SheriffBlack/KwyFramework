using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

/// <summary>
/// Composes protocol creators during application startup and produces an immutable factory.
/// </summary>
public sealed class CommunicationFactoryBuilder : ICommunicationFactoryRegistry
{
    private readonly Dictionary<Type, ICommunicationClientCreator> creators = new();
    private bool built;

    public void AddCreator(ICommunicationClientCreator creator)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(creator);

        if (!creators.TryAdd(creator.ConfigType, creator))
        {
            throw new InvalidOperationException(
                $"A communication creator for configuration type '{creator.ConfigType.FullName}' is already registered.");
        }
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
}

/// <summary>Convenience registration helpers for delegate-based protocol creators.</summary>
public static class CommunicationFactoryRegistryExtensions
{
    public static ICommunicationFactoryRegistry RegisterCreator<TConfig>(
        this ICommunicationFactoryRegistry registry,
        Func<TConfig, ICommunicationClient> creator)
        where TConfig : class, IProtocolConfig
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(creator);
        registry.AddCreator(new DelegateCommunicationClientCreator<TConfig>(creator));
        return registry;
    }

    private sealed class DelegateCommunicationClientCreator<TConfig>(
        Func<TConfig, ICommunicationClient> creator) : ICommunicationClientCreator<TConfig>
        where TConfig : class, IProtocolConfig
    {
        public ICommunicationClient Create(TConfig config) => creator(config);
    }
}
