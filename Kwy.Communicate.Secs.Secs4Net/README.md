# Kwy.Communicate.Secs.Secs4Net

`Kwy.Communicate.Secs.Secs4Net` 是 `Secs4Net` 到 Kwy `ISecsClient` 的适配器。

分层关系：

```text
Kwy.Communicate.Secs
  定义 ISecsClient、SecsMessage、SecsItem。

Kwy.Communicate.Secs.Secs4Net
  引用 Secs4Net，实现真实 HSMS/SECS 通信适配。

Kwy.Communicate.Gem / Gem300
  只依赖 Kwy.Communicate.Secs，不直接依赖 Secs4Net。
```

使用方式：

```csharp
// 业务项目中先按 Secs4Net 官方方式注册 ISecsGem。
// 然后把 ISecsGem 适配成 Kwy 的 ISecsClient：
services.AddSingleton<ISecsClient>(provider =>
    new Secs4NetSecsClient(provider.GetRequiredService<Secs4Net.ISecsGem>()));
```

生命周期：

- `Secs4NetSecsClient` 接入 `CommunicationClientBase`，统一使用 Kwy 的连接状态、错误事件、断开清理和后台重连语义。
- `ConnectAsync()` 会等待 Secs4Net 连接进入 `Connected` 或 `Selected` 后才认为 Kwy 客户端已连接。
- 发送失败或 Primary 接收循环异常会进入统一故障流程，并由基类触发后台单飞重连。

限制：

- 当前 Secs4Net 公开 API 未暴露可稳定读写的 `SystemBytes` 属性，Kwy 消息从 Secs4Net 转回时无法保留原始 System Bytes。事务关联应优先依赖 Secs4Net 内部请求/响应机制；如果项目需要完整 SML/header 级日志，需要在更底层增加专用适配。

Secs4Net 官方仓库：https://github.com/mkjeff/secs4net
