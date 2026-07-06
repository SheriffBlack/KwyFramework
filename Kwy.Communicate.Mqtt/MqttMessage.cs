namespace Kwy.Communicate.Mqtt;

public sealed record MqttMessage(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    byte QualityOfServiceLevel = 0,
    bool Retain = false);
