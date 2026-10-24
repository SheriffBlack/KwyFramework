using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

public sealed class ProtocolClientFactory : IProtocolClientFactory
{
    private readonly IReadOnlyDictionary<Type, IProtocolClientCreator> creators;

    public ProtocolClientFactory(IEnumerable<IProtocolClientCreator> creators)
    {
        ArgumentNullException.ThrowIfNull(creators);
        var byConfigType = new Dictionary<Type, IProtocolClientCreator>();
        foreach (IProtocolClientCreator creator in creators)
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
                    $"A protocol client creator for configuration type '{creator.ConfigType.FullName}' is already registered.");
            }
        }

        this.creators = byConfigType;
    }

    public IProtocolClient CreateClient(IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ValidateAndThrow();
        Type configType = config.GetType();
        if (!creators.TryGetValue(configType, out IProtocolClientCreator? creator))
        {
            IProtocolClientCreator[] compatibleCreators = creators
                .Where(pair => pair.Key.IsAssignableFrom(configType))
                .Select(pair => pair.Value)
                .ToArray();

            creator = compatibleCreators.Length switch
            {
                0 => throw new NotSupportedException($"Unregistered protocol configuration type: {configType.Name}"),
                1 => compatibleCreators[0],
                _ => throw new InvalidOperationException(
                    $"Multiple protocol client creators can handle configuration type '{configType.FullName}'. Register an exact creator for that type.")
            };
        }

        return creator.Create(config)
            ?? throw new InvalidOperationException(
                $"Protocol client creator for '{configType.FullName}' returned null.");
    }

    public TClient Create<TClient, TConfig>(TConfig config)
        where TClient : class, IProtocolClient
        where TConfig : IProtocolConfig
    {
        var client = CreateClient(config);
        if (client is TClient typedClient)
            return typedClient;

        client.Dispose();
        throw new InvalidCastException($"Registered creator returned {client.GetType().Name}, not {typeof(TClient).Name}.");
    }
}
