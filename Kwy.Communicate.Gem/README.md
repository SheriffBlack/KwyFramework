# Kwy.Communicate.Gem

`Kwy.Communicate.Gem` 是 SEMI E30 GEM 行为层，直接使用 Secs4Net 原生消息类型。
`SecsGemClient` 通过 `CommunicationClientBase` 接入 Kwy 统一通信生命周期。

当前包以 `preview` 形式发布，使用 MIT 许可证。预发布标识表示公共 API 和设备互操作行为
仍可能根据实际 Host/Equipment 集成反馈调整，与开源许可证无关。本项目不代表通过 SEMI 认证。

协议已知时，通过协议专属 Factory 创建客户端：

```csharp
var config = new SecsGemClientConfig
{
    Host = "192.168.1.100",
    Port = 5000,
    DeviceId = 0,
    IsActive = true
};

await using ISecsGemClient client = SecsGemClientFactory.Create(config);
await client.ConnectAsync(cancellationToken);
```

`Kwy.Communicate.Gem` 不引用 DI 容器，也不提供 `IServiceCollection` 扩展。如果宿主使用 DI，由应用组合根决定是否注册 Factory 创建的实例；客户端的断开与释放责任仍必须唯一且明确。

Secs4Net 的原生构造 API 使用 `Microsoft.Extensions.Options` / `IOptions<T>`，所以 NuGet 依赖树中会传递出现 DI Abstractions。Kwy GEM 的公共入口仍然是容器无关的 `SecsGemClientFactory`，不要求使用者引入或配置 DI 容器。

需要默认 GEM 行为服务时，也在应用层显式组装：

```csharp
var registry = new GemRegistry();
var equipment = new GemEquipmentService(client, registry);
```

当前模块提供：

- GEM 通信状态和控制状态
- Alarm / Event / Report / Variable / Equipment Constant 模型
- Recipe / PPID 模型
- Remote Command 模型
- CEID / RPTID / VID / ALID / ECID 标准标识模型
- Trace / Spooling / History 基础服务
- Host / Equipment 角色上下文
- `GemRegistry` 注册表
- `GemEquipmentService` 默认服务
- S5F1、S6F11、S10F1 等常用消息工厂

设计边界：

```text
Gem
  负责 E30 行为模型和 SxFy 语义。

Gem300
  负责 Carrier、Substrate、ProcessJob、ControlJob 等 300mm 对象模型。
```
