using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Mqtt;

/// <summary>MQTT 发布、接收与订阅操作。</summary>
public interface IMqttCommunication : IMessageClient<MqttMessage>
{
    /// <summary>获取因接收缓冲区已满而丢弃的消息数量。</summary>
    long DroppedMessageCount { get; }

    /// <summary>订阅一个或多个主题筛选器，并记录成功订阅项以便重连后恢复。</summary>
    Task SubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);

    /// <summary>取消订阅一个或多个主题筛选器。</summary>
    Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);

    /// <summary>获取将在重连后恢复的订阅项快照。</summary>
    IReadOnlyList<string> GetSubscribedTopics();
}
