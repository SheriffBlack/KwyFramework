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
        Type configType = config.GetType();
        if (!creators.TryGetValue(configType, out ICommunicationClientCreator? creator))
        {
            ICommunicationClientCreator[] compatibleCreators = creators
                .Where(pair => pair.Key.IsAssignableFrom(configType))
                .Select(pair => pair.Value)
                .ToArray();

            creator = compatibleCreators.Length switch
            {
                0 => throw new NotSupportedException($"Unregistered protocol configuration type: {configType.Name}"),
                1 => compatibleCreators[0],
                _ => throw new InvalidOperationException(
                    $"Multiple communication creators can handle configuration type '{configType.FullName}'. Register an exact creator for that type.")
            };
        }

        return creator.Create(config)
            ?? throw new InvalidOperationException(
                $"Communication creator for '{configType.FullName}' returned null.");
    }

    public TCommunication Create<TCommunication, TConfig>(TConfig config)
        where TCommunication : class, ICommunicationClient
        where TConfig : IProtocolConfig
    {
        var client = CreateClient(config);
        if (client is TCommunication typedClient)
            return typedClient;

        client.Dispose();
        throw new InvalidCastException($"Registered creator returned {client.GetType().Name}, not {typeof(TCommunication).Name}.");
    }
}
