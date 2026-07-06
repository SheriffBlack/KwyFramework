# Kwy.Mes 架构说明

`Kwy.Mes` 面向设备业务层，封装工单、过站、配方、测试结果和追溯上传等 MES 语义。它不直接规定现场必须使用 HTTP、MQTT、SECS/GEM 或数据库。

## 项目分层

- `Kwy.Mes.Abstractions`
  - 只放 MES 业务接口、状态、事件和数据模型。
  - 不依赖 `Kwy.Communicate.*`，避免把业务语义绑死到某一种通信方式。
- `Kwy.Mes.Core`
  - 提供 `MesServiceBase`、JSON 工具和 `SimulationMesService`。
  - 适合模板、离线调试和自定义 MES 服务复用。
- `Kwy.Mes.Http`
  - 基于 `IRequestClient<HttpRequestMessage, HttpResponseMessage>` 适配 HTTP MES。
  - 默认 JSON 协议可用 `IHttpMesMessageMapper` 替换。
- `Kwy.Mes.Mqtt`
  - 基于 `IMessageClient<MqttMessage>` 适配 MQTT MES。
  - 支持结果/追溯发布，也支持配置响应 Topic 后进行 request/reply。

## 常用接口

- `IMesService`：完整 MES 门面，适合业务层直接注入。
- `IMesWorkOrderService`：工单读取。
- `IMesRouteService`：过站/路由检查。
- `IMesRecipeService`：配方读取。
- `IMesResultService`：测试结果上传。
- `IMesTraceService`：追溯数据上传。

## 使用建议

业务层优先依赖 MES 语义接口，而不是直接依赖 HTTP/MQTT：

```csharp
public sealed class TestFlow
{
    private readonly IMesService mes;

    public TestFlow(IMesService mes)
    {
        this.mes = mes;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await mes.ConnectAsync(cancellationToken);

        var route = await mes.CheckRouteAsync(
            new MesUnit("SN001", WorkOrderNo: "WO001"),
            new MesStation("EQP01", "ST01"),
            cancellationToken);

        if (!route.Succeeded || route.Data?.Allowed != true)
        {
            return;
        }
    }
}
```

如果现场 MES 的字段名和默认 JSON 不一致，在业务项目中实现对应 Mapper 即可，不需要修改 Kwy 框架。
