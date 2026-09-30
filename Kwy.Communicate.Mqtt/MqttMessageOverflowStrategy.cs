namespace Kwy.Communicate.Mqtt;

/// <summary>接收缓冲区已满时，控制传入消息的处理方式。</summary>
public enum MqttMessageOverflowStrategy
{
    /// <summary>等待缓冲区可用，对 MQTT 接收回调施加背压。</summary>
    Wait,

    /// <summary>丢弃缓冲区中最早的消息，保留最新消息。</summary>
    DropOldest,

    /// <summary>丢弃新接收的消息。</summary>
    DropNewest
}
