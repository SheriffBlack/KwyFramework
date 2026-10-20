# 雷赛运动控制适配器结构

`Kwy.Device.Motion.Leadshine.Dmc` 是雷赛 DMC 脉冲卡产品族程序集。具体设备必须按“产品族/API 兼容边界”拆分，不应将 DMC-E 总线卡或 EMC 外置控制器放入本程序集。

## 当前实现

- `Dmc3800Device`：DMC3800 系列脉冲卡适配器。
- `Dmc3800Config`：DMC3800 系列硬件配置。
- `AddLeadshineDmc3800`：推荐的依赖注入入口。

## 后续产品族

新增设备时建立并列的产品族命名空间，不要继承 DMC3800 具体适配器：

- `Leadshine.DmcE`：DMC-E EtherCAT 总线卡。
- `Leadshine.Emc`：EMC 外置式运动控制器。

产品族可共用错误码、基础数据类型等纯托管代码，但不共用包含产品语义的 P/Invoke 门面或设备生命周期。

## 32/64 位原生库

位数是运行时部署维度，不是产品族，因此不创建 `Dmc3800X86Device` 或 `Dmc3800X64Device`。正式发布时应使用同一托管适配器，分别携带：

```text
runtimes/win-x86/native/LTDMC.dll
runtimes/win-x64/native/LTDMC.dll
```

并分别使用 `win-x86` / `win-x64` 发布和验证。当前仓库中的 `DLL/LTDMC.dll` 为 x86，因此当前集成只能作为 x86 部署。在取得并验证雷赛官方 x64 库之前，不宣称 x64 可用。
