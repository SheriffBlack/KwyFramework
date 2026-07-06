namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Creates communication clients from protocol configurations.
/// </summary>
public interface ICommunicationFactory
{
    ICommunicationClient CreateClient(IProtocolConfig config);

    TCommunication Create<TCommunication, TConfig>(TConfig config)
        where TCommunication : class, ICommunicationClient
        where TConfig : IProtocolConfig;

    void RegisterCreator<TConfig>(Func<TConfig, ICommunicationClient> creator)
        where TConfig : IProtocolConfig;
}
