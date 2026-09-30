namespace Kwy.Communicate.Mqtt;

/// <summary>表示一条 MQTT 应用消息。</summary>
/// <param name="Topic">发布或接收的主题。</param>
/// <param name="Payload">二进制应用负载。</param>
/// <param name="QualityOfServiceLevel">MQTT 服务质量等级，取值为 0 至 2。</param>
/// <param name="Retain">Broker 是否应保留已发布消息。</param>
public sealed record MqttMessage(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    byte QualityOfServiceLevel = 0,
    bool Retain = false);
