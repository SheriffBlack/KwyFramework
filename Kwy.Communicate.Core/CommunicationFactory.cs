using Kwy.Communicate.Abstractions;
using System.Collections.Concurrent;

namespace Kwy.Communicate.Core;

public sealed class CommunicationFactory : ICommunicationFactory
{
    private readonly ConcurrentDictionary<Type, Func<IProtocolConfig, ICommunicationClient>> creators = new();

    public void RegisterCreator<TConfig>(Func<TConfig, ICommunicationClient> creator)
        where TConfig : IProtocolConfig
    {
        ArgumentNullException.ThrowIfNull(creator);
        creators[typeof(TConfig)] = config => creator((TConfig)config);
    }

    public ICommunicationClient CreateClient(IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (creators.TryGetValue(config.GetType(), out var creator))
            return creator(config);

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
