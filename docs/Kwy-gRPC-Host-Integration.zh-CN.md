# Kwy gRPC Host 接入指南

`Kwy.Communicate.Grpc.Contracts`、`.Client`、`.Service` 是通信基础设施。它们不包含 PLC、运动卡、相机或业务 RPC；设备项目负责定义自己的 `Xxx.Contracts` 与 Host。

## 1. Host：注册与映射基础服务

若 Client 使用 `GrpcCommunication.ConnectAsync()`，Host 必须暴露 Kwy 的 `ConnectivityService.Ping`，否则连接确认会失败。

```csharp
using Kwy.Communicate.Grpc.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKwyGrpc();
builder.Services.AddSingleton<EquipmentGrpcService>(); // 业务服务

var app = builder.Build();
app.MapKwyGrpcConnectivity();
app.MapGrpcService<EquipmentGrpcService>();
app.Run();
```

`AddKwyGrpc` 只注册 Kwy 的 gRPC 基础设施；`MapKwyGrpcConnectivity` 只映射 Kwy 的 Ping 等连通性端点。业务 RPC 必须逐个使用 ASP.NET Core 原生 `MapGrpcService<TService>()` 映射。

## 2. Windows Named Pipe：Host 决定访问控制

```csharp
builder.WebHost.UseKwyGrpcNamedPipe(
    pipeName: "Kwy.Equipment.Line01.Control",
    configureTransport: options =>
    {
        // 仅适用于 HMI 与 Host 使用同一 Windows 用户、且权限级别相同的场景。
        options.CurrentUserOnly = true;

        // 多用户、Windows Service、MES 等场景：不要启用 CurrentUserOnly。
        // 应由部署项目在这里设置 PipeSecurity，授予明确的用户或组最小权限。
    });
```

`CurrentUserOnly = true` 会同时校验 Windows 用户与提升权限级别；因此“管理员启动 Host、普通用户启动 HMI”会被拒绝。跨身份访问时，应由业务部署项目根据实际服务账号和用户组设置 `PipeSecurity`，框架不应猜测或默认放开权限。

Kestrel 的 Named Pipe 传输选项提供 `CurrentUserOnly` 与 `PipeSecurity`；未设置 `PipeSecurity` 不应被视为已完成安全设计。[Microsoft NamedPipeTransportOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.transport.namedpipes.namedpipetransportoptions?view=aspnetcore-10.0)

## 3. TCP/HTTPS：由 Host 配置证书和入口

```csharp
using Microsoft.AspNetCore.Server.Kestrel.Core;

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, endpoint =>
    {
        endpoint.UseHttps(); // 从证书存储或受保护配置加载证书
        endpoint.Protocols = HttpProtocols.Http2;
    });
});
```

框架不应固化端口、证书位置、认证方案或防火墙规则。跨机器服务还应由业务 Host 增加认证、授权与网络隔离。

## 4. Client：连接确认和业务 Deadline 是两回事

```csharp
var communication = new GrpcCommunication(new GrpcConfig
{
    Transport = GrpcTransport.NamedPipe,
    Endpoint = "Kwy.Equipment.Line01.Control",
    Timeout = 5_000 // 仅用于 ConnectAsync 中的 Ping
});

await communication.ConnectAsync(cancellationToken);

var client = communication.CreateClient(
    invoker => new EquipmentService.EquipmentServiceClient(invoker));

var options = communication.CreateCallOptions(
    deadline: TimeSpan.FromSeconds(2),
    cancellationToken: cancellationToken);

var reply = await client.GetStatusAsync(new GetStatusRequest(), options);
```

每个业务 RPC 都应有合适的 Deadline。读取状态可较短；加载配方、文件上传、长时间任务应使用不同 Deadline 或改为“提交任务 + 查询/订阅进度”。

`GrpcCommunication` 不会自动重试业务 RPC。尤其是 `MoveAxis`、写 PLC、启动工艺等控制命令，超时可能代表“Host 已收到但 Reply 丢失”；是否重试必须由业务层结合 `request_id`、命令状态、设备互锁决定。

## 5. 推荐依赖关系

```text
Xxx.HMI.WPF ────────────── Kwy.Communicate.Grpc.Client
       │                                  │
       └──────────────────────────── Xxx.Equipment.Contracts

Xxx.Equipment.Host ─────── Kwy.Communicate.Grpc.Service
       │                                  │
       ├────────────────────────── Kwy.Communicate.Grpc.Contracts
       └────────────────────────── Xxx.Equipment.Contracts
```

`Xxx.HMI.WPF` 不引用 `.Service`、不引用设备 SDK；`Kwy` 通用 Contracts 也不放 `MoveAxis`、`ReadPlcRegister` 等设备业务语义。
