# KwyTemplate.Flow

`KwyTemplate.Flow` 是模板项目中的业务流程层。它负责描述不同客户、不同机种、不同工艺流程需要哪些设备，以及这些设备在流程中如何使用。

设备的创建、连接、断开和生命周期不放在本层，而由 `KwyTemplate.Device` 统一处理。本层只通过 `DeviceId` 使用已经接入的设备。

## 与 Device 层的关系

```text
KwyTemplate.Device
  读取 device-connections.json
  按 DeviceType 找 IDeviceConnectionFactory
  创建设备
  注册到 IDeviceRegistry
  连接设备

KwyTemplate.Flow
  声明当前机种需要哪些 DeviceId
  激活机种设备 Profile
  从 IDeviceRegistry 中取设备
  编写工艺动作
```

这样设计后，后续同事面对不同客户时，通常只需要改 `KwyTemplate.Flow`：

- 新增机种 Profile。
- 新增设备角色。
- 新增流程步骤。
- 使用不同的 `DeviceId` 组合设备。

一般不需要修改 `KwyTemplate.Device`。

## 多个相同设备

多个同品牌、同型号、同类型设备不能靠接口类型区分，必须靠 `DeviceId` 区分。

例如两台 HSL PLC：

```text
PLC.Main
PLC.Loader
```

它们的 `DeviceType` 都可以是 `HslPlc`，但 `DeviceId` 不同。

在 Flow 中声明：

```csharp
var profile = new MachineDeviceProfile
{
    Name = "DefaultMachine",
    Devices =
    [
        new MachineDeviceRequirement
        {
            Role = "MainPlc",
            DeviceId = "PLC.Main",
            DisplayName = "主 PLC"
        },
        new MachineDeviceRequirement
        {
            Role = "LoaderPlc",
            DeviceId = "PLC.Loader",
            DisplayName = "上料 PLC"
        }
    ]
};
```

激活后使用：

```csharp
await machineDeviceResolver.ActivateAsync(profile, cancellationToken);

var mainPlc = machineDeviceResolver.GetRequiredDevice<IPlcDevice>("MainPlc");
var loaderPlc = machineDeviceResolver.GetRequiredDevice<IPlcDevice>("LoaderPlc");
```

## 一个设备多个角色

`Role` 必须唯一，但 `DeviceId` 可以重复。

这允许一个物理设备承担多个业务角色：

```csharp
Devices =
[
    new MachineDeviceRequirement { Role = "MainPlc", DeviceId = "PLC.Main" },
    new MachineDeviceRequirement { Role = "AlarmPlc", DeviceId = "PLC.Main" }
]
```

这种写法表示两个业务角色都使用同一个物理 PLC。

## 推荐使用方式

业务流程中优先通过角色取设备：

```csharp
var plc = machineDeviceResolver.GetRequiredDevice<IPlcDevice>("MainPlc");
```

只有在非常明确、不需要机种映射时，才直接通过设备 ID 取：

```csharp
var plc = machineDeviceResolver.GetRequiredDevice<IPlcDevice>("PLC.Main");
```
