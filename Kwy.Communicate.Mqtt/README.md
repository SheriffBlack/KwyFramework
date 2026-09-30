# Kwy.Communicate.Mqtt

Container-independent MQTT client for `Kwy.Communicate`, powered by MQTTnet.

The package does not depend on Microsoft dependency injection. Construct the client directly when the protocol is known:

```csharp
var config = new MqttConfig
{
    Host = "broker.example.com",
    Port = 8883,
    ClientId = "machine-01",
    UseTls = true,
    SubscribeTopics = new[] { "factory/machine-01/commands" }
};

await using var mqtt = new MqttCommunication(config);
await mqtt.ConnectAsync();
await mqtt.PublishAsync("factory/machine-01/status", "online"u8.ToArray());
```

For applications that select a protocol from configuration at runtime, register only the MQTT creator:

```csharp
var factory = new CommunicationFactoryBuilder()
    .RegisterMqtt()
    .Build();
```

`DangerousAcceptAnyServerCertificate` defaults to `false`. Enable it only in a controlled test environment. Incoming messages are available through both `MessageReceived` and `ReadMessagesAsync`. Configure `MessageOverflowStrategy` for backpressure or message dropping, and inspect `DroppedMessageCount` when using a drop strategy.

Create the NuGet and symbol packages with:

```powershell
dotnet pack Kwy.Communicate.Mqtt\Kwy.Communicate.Mqtt.csproj -c Release -o artifacts\nuget
```
