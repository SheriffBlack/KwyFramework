# KwyTemplate.Device

`KwyTemplate.Device` 是模板项目中的设备接入层。它只负责设备连接配置、配置持久化、设备创建、连接/断开、启动连接，以及把设备注册到 `Kwy.Device.Core` 提供的 `IDeviceRegistry` 中。

具体业务逻辑不要放在这里，例如 PLC 点位、工艺流程、按钮动作、报警判断、视觉协议解析等。这些内容应放在 `KwyTemplate.Flow` 或具体业务模块中。

## 分层定位

```text
KwyTemplate.Device
  设备连接配置
  配置持久化
  设备连接工厂
  设备连接/断开编排
  注册到 IDeviceRegistry

KwyTemplate.Flow
  机种/客户流程
  声明当前机种需要哪些 DeviceId
  从 IDeviceRegistry / IMotionRuntimeRegistry 获取设备
  编写具体业务动作

KwyTemplate.App
  UI 展示
  参数编辑
  手动连接/断开
  状态显示
```

## 核心设计

设备配置使用集合模型，而不是为每个设备写一个固定属性：

```csharp
public sealed class DeviceConnectionOptions
{
    public List<DeviceConnectionEntry> Devices { get; set; } = [];
}

public sealed class DeviceConnectionEntry
{
    public string DeviceId { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool ConnectOnStartup { get; set; }
    public object? Config { get; set; }
}
```

这样后续设备多起来时，不需要继续修改 `DeviceConnectionOptions`，只需要新增一条设备配置，并提供对应的 `IDeviceConnectionFactory`。

配置文件路径：

```text
{AppContext.BaseDirectory}/Config/device-connections.json
```

## 设备标识

设备实例必须用 `DeviceId` 区分，不用类型区分。

例如同一个项目里有两台 HSL PLC：

```json
{
  "devices": [
    {
      "deviceId": "PLC.Main",
      "deviceType": "HslPlc",
      "displayName": "主PLC",
      "enabled": true,
      "connectOnStartup": true,
      "config": {}
    },
    {
      "deviceId": "PLC.Loader",
      "deviceType": "HslPlc",
      "displayName": "上料PLC",
      "enabled": true,
      "connectOnStartup": false,
      "config": {}
    }
  ]
}
```

两条记录的 `DeviceType` 都是 `HslPlc`，但 `DeviceId` 不同，因此业务层可以稳定地区分它们。

## 连接工厂

每种需要通过模板配置创建的设备类型，都应该提供一个 `IDeviceConnectionFactory`：

```csharp
public interface IDeviceConnectionFactory
{
    string DeviceType { get; }

    IDevice Create(DeviceConnectionEntry entry);

    bool IsSameDevice(IDevice device, DeviceConnectionEntry entry);

    DeviceConnectionConfigurationSection CreateConfigurationSection(DeviceConnectionEntry entry);
}
```

内置 Kwy 设备也需要工厂，但它只是很薄的一层适配：

```text
HslPlcConnectionOptions
  -> HslPlcConfig
  -> HslPlcDevice
```

系统设置页也不直接认识 `HslPlcConnectionOptions`、`ExternalTcpDeviceConnectionOptions` 等具体类型，而是通过工厂提供配置展示模型：

```csharp
public sealed class DeviceConnectionConfigurationSection
{
    public string Title { get; }
    public string Description { get; }
    public object Source { get; }
}
```

`Source` 会交给 `KwyPropertyGrid` 渲染。因此后续项目新增设备时，`SystemViewModel` 不需要跟着增加 `switch DeviceType`。

## 系统设置页如何拿到具体配置类型

`CreateConfigurationSection()` 返回的是统一的 `DeviceConnectionConfigurationSection`，但其中的 `Source` 是 `object`，可以承载具体设备的强类型配置对象。

以 HSL PLC 为例：

```csharp
public DeviceConnectionConfigurationSection CreateConfigurationSection(DeviceConnectionEntry entry)
{
    HslPlcConnectionOptions config = entry.GetConfig<HslPlcConnectionOptions>();

    return new DeviceConnectionConfigurationSection(
        config.DeviceName,
        "配置 PLC 的品牌、连接方式、网络/串口参数、启动策略和心跳参数。",
        config);
}
```

调用链如下：

```text
SystemViewModel
  -> 遍历 DeviceConnectionEntry
  -> 根据 entry.DeviceType 找到 HslPlcConnectionFactory
  -> 调用 factory.CreateConfigurationSection(entry)

HslPlcConnectionFactory
  -> entry.GetConfig<HslPlcConnectionOptions>()
  -> 得到强类型 HslPlcConnectionOptions
  -> 塞进 DeviceConnectionConfigurationSection.Source

SystemView.xaml
  -> KwyPropertyGrid Source="{Binding Source}"
  -> 根据 Source.GetType() 读取真实运行时类型
  -> 按 HslPlcConnectionOptions 的属性和元数据生成 UI
```

所以 `SystemViewModel` 不需要知道具体类型，Factory 知道具体类型，`KwyPropertyGrid` 根据 `Source` 的真实运行时类型生成界面。

外部自定义设备通常需要两部分：

```text
自定义 IDevice / DeviceBase 实现
自定义 IDeviceConnectionFactory
```

## 启动连接

启动时不会无脑连接所有设备，只连接满足以下条件的设备：

```text
Enabled == true
ConnectOnStartup == true
```

这让模板可以保留多台设备的配置，但只启动当前项目实际需要的部分。

## 连接服务

`IDeviceConnectionService` 提供按 `DeviceId` 的通用连接入口：

```csharp
Task ConnectStartupDevicesAsync(CancellationToken cancellationToken = default);
Task ConnectAllAsync(CancellationToken cancellationToken = default);
Task DisconnectAllAsync(CancellationToken cancellationToken = default);

Task ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
Task ConnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default);
Task DisconnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default);
```

业务流程和 UI 都应该使用按 `DeviceId` 的方法，不再保留 `ConnectMainPlcAsync`、`ConnectExternalTcpDeviceAsync` 这类固定设备快捷方法。

## Flow 中使用设备

`KwyTemplate.Flow` 通过 `MachineDeviceProfile` 声明当前机种需要的设备：

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
            DisplayName = "主PLC"
        },
        new MachineDeviceRequirement
        {
            Role = "LoaderPlc",
            DeviceId = "PLC.Loader",
            DisplayName = "上料PLC"
        }
    ]
};

await machineDeviceResolver.ActivateAsync(profile, cancellationToken);
```

使用设备时推荐按业务角色获取：

```csharp
var plc = machineDeviceResolver.GetRequiredDevice<IPlcDevice>("MainPlc");
await plc.WriteBoolAsync("R100", true, cancellationToken);
```

也可以直接使用框架注册表：

```csharp
var plc = deviceRegistry.GetRequiredDevice<IPlcDevice>("PLC.Main");
```

## 新增设备类型

新增一种设备类型时，通常只需要：

1. 定义连接配置模型。
2. 实现 `IDeviceConnectionFactory`。
3. 在 `DeviceModule` 中注册该工厂。
4. 在 `device-connections.json` 中添加设备条目。
5. 在 `KwyTemplate.Flow` 的机种 Profile 中引用对应 `DeviceId`。

如果只是同类型增加一台设备，不需要新增工厂，只需要新增一条不同 `DeviceId` 的配置。
