# Kwy.Device.Core

这是设备模块的跨领域运行时，不是 IO、运动、PLC、相机或仪表的公共杂物箱。

## 当前职责

- `DeviceBase`：统一设备连接、断开、状态事件、异常上报与释放资源的生命周期。
- `DeviceRegistry`：按稳定设备 ID 或能力接口保存和查找已经创建的设备实例。
- `AddDeviceCore`：注册跨领域基础服务。

## 放入此项目的判断

一个能力必须同时满足以下条件，才可以进入本项目：

1. 至少两个以上设备领域都需要；
2. 不需要了解轴、IO 点、PLC 地址、图像、测量值等领域模型；
3. 不依赖厂商 SDK；
4. 不会迫使所有领域为了一个可选能力增加依赖。

否则应归属到 `Kwy.Device.{Domain}.Core`。例如逻辑 IO、回零可信度、PLC 点位轮询、相机采集会话和仪表结果标准化，都属于领域 Core。

## 依赖关系

```text
Kwy.Device.{Domain}.Abstractions
            ↑
Kwy.Device.Abstractions ← Kwy.Device.Core
            ↑                    ↑
Kwy.Device.{Domain}.Core ────────┘
            ↑
Kwy.Device.{Domain}.{Vendor}
```

完整约定见 [DEVICE_MODULE_CONVENTIONS.md](../Kwy.Device.Abstractions/DEVICE_MODULE_CONVENTIONS.md)。
