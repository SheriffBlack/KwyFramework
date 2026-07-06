using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Mqtt;

public interface IMqttCommunication : IMessageClient<MqttMessage>
{
    Task SubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetSubscribedTopics();
}
