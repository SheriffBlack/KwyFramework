using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

public sealed class CommunicationFactory : ICommunicationFactory
{
    private readonly IReadOnlyDictionary<Type, ICommunicationClientCreator> creators;

    public CommunicationFactory(IEnumerable<ICommunicationClientCreator> creators)
    {
        ArgumentNullException.ThrowIfNull(creators);
        var byConfigType = new Dictionary<Type, ICommunicationClientCreator>();
        foreach (ICommunicationClientCreator creator in creators)
        {
            ArgumentNullException.ThrowIfNull(creator);
            if (!typeof(IProtocolConfig).IsAssignableFrom(creator.ConfigType))
            {
                throw new ArgumentException(
                    $"Creator configuration type '{creator.ConfigType.FullName}' does not implement {nameof(IProtocolConfig)}.",
                    nameof(creators));
            }

            if (!byConfigType.TryAdd(creator.ConfigType, creator))
            {
                throw new InvalidOperationException(
                    $"A communication creator for configuration type '{creator.ConfigType.FullName}' is already registered.");
            }
        }

        this.creators = byConfigType;
    }

    public ICommunicationClient CreateClient(IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ValidateAndThrow();
        if (creators.TryGetValue(config.GetType(), out ICommunicationClientCreator? creator))
            return creator.Create(config)
                ?? throw new InvalidOperationException(
                    $"Communication creator for '{config.GetType().FullName}' returned null.");

        throw new NotSupportedException($"Unregistered protocol configuration type: {config.GetType().Name}");
    }

    public TCommunication Create<TCommunication, TConfig>(TConfig config)
        where TCommunication : class, ICommunicationClient
        where TConfig : IProtocolConfig
    {
        var client = CreateClient(config);
        return client as TCommunication
            ?? throw new InvalidCastException($"Registered creator returned {client.GetType().Name}, not {typeof(TCommunication).Name}.");
    }
}
