# Kwy.Communicate.OpcUa

`Kwy.Communicate.OpcUa` 是不依赖依赖注入容器的 OPC UA 客户端库，提供连接生命周期、
自动重连、节点读写、订阅恢复、有界消息流和可选运行时 Factory。

```csharp
var config = new OpcUaConfig
{
    EndpointUrl = "opc.tcp://127.0.0.1:4840",
    SecurityMode = MessageSecurityMode.None,
    SecurityPolicy = SecurityPolicies.None,
    SubscribeNodes = ["ns=2;s=Machine.Status"]
};

await using var client = new OpcUaCommunication(config);
await client.ConnectAsync();
object? value = await client.ReadNodeAsync<object>("ns=2;s=Machine.Status");
```

生产环境建议使用 `SignAndEncrypt` 和受支持的安全策略，通过 `PkiRootPath` 下的信任库管理
服务器证书。`AutoAcceptUntrustedCertificates` 默认关闭；确需启用时也只接受
`BadCertificateUntrusted`，不会绕过过期、主机名或用途等其他证书错误。

只有在协议由配置动态选择时才需要注册 Factory：

```csharp
ICommunicationFactory factory = new CommunicationFactoryBuilder()
    .RegisterOpcUa()
    .Build();
```
