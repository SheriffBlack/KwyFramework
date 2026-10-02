# Kwy.Device.PLC.Beckhoff

基于 `Beckhoff.TwinCAT.Ads` 的 TwinCAT PLC 适配器。

业务层通过 `PlcPointDefinition.Id` 和 `ILogicalPlcReader` / `ILogicalPlcWriter` 访问点位；点位的 `Address` 使用 TwinCAT PLC 符号名，例如 `MAIN.Transport.bFixturePresent`。

```csharp
services.AddBeckhoffPLC("plc.main", "主控 TwinCAT", options =>
{
    options.AmsNetId = "192.168.10.20.1.1";
    options.AmsPort = 851;
});

services.AddPLCPointDefinitions([
    new PlcPointDefinition
    {
        Id = "transport.fixture.present",
        Name = "治具到位",
        DeviceId = "plc.main",
        Address = "MAIN.Transport.bFixturePresent",
        DataType = PlcDataType.Boolean,
        Access = PlcPointAccess.ReadOnly
    }
]);
```

`PlcPointDefinition` 由 PLC Core 统一维护，可以同时描述多台 PLC；此适配器只负责把 `Address` 解释为 ADS 符号名。`BeckhoffAdsPlcConfig` 继承 `PlcConfig`，复用心跳等通用配置，并补充 AMS Net ID、ADS 端口与操作超时；TCP/串口字段不适用于 ADS。

需要先在运行主机和 TwinCAT 目标设备间正确配置 ADS Route。此项目不依赖 `Beckhoff.TwinCAT.Ads.Reactive`；常规 PLC 点位读写及通知可由基础 ADS API 完成。高频实时安全与运动控制应留在 TwinCAT Runtime / Safety 控制链路，不应依赖上位机 ADS 通讯。
