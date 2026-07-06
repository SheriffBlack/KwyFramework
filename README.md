# Kwy

Kwy 是一套面向工业设备软件的模块化 .NET 框架，主要用于构建设备控制软件、自动化系统、机器视觉工具以及基于 WPF 的工程应用。

它关注工业软件项目中会反复遇到的基础能力：通信、设备抽象、MVVM、WPF UI、日志、文件处理、授权、视觉算法以及可直接复用的项目模板。

## 项目概览

工业设备软件通常不只是协议读写。一个可维护的项目还需要设备生命周期管理、运行状态同步、安全联锁、权限控制、配置编辑、统一 UI 风格、日志、文件处理以及便于交付的打包方式。

Kwy 将这些能力拆分为多个职责清晰、便于 NuGet 发布和按需引用的模块。

```text
Kwy.Communicate.*     通信协议与传输客户端
Kwy.Device.*          工业设备抽象、状态同步与运行时管理
Kwy.UI.WPF.*          WPF 主题、控件样式和可复用组件
Kwy.MVVM.*            MVVM、模块化、区域导航、权限和消息总线
Kwy.Files.*           JSON、INI、Excel 等文件处理能力
Kwy.Logging.*         日志抽象与 Serilog 集成
Kwy.Licensing.*       授权、商业库激活和密码狗抽象
Kwy.Vision.*          视觉抽象、几何模型和算法封装
KwyTemplate.*         面向设备软件的应用模板
```

## 核心能力

### 通信模块

- TCP / Serial
- MQTT
- OPC UA
- 基于 FluentModbus 的 Modbus 通信
- NI GPIB
- SECS / GEM / GEM300 抽象

### 设备模块

- PLC
- IO 卡
- 运动控制卡
- 相机
- 仪表
- 基于 `DeviceId` 的设备注册与查找
- 状态同步、安全联锁和恢复策略基础设施

### WPF UI 模块

- Light / Dark 主题
- 常用控件默认样式
- Dialog 消息弹窗服务
- Toast 轻提示服务
- 基于元数据生成的属性编辑器
- FlowDesigner 流程节点编辑基础能力

### MVVM 模块

- BindableBase 基类
- 区域导航
- 模块化应用结构
- Dialog 抽象
- 权限系统
- 基于 CommunityToolkit.Mvvm 的消息总线封装

### 文件模块

- JSON 辅助能力
- INI 文件支持
- Excel 抽象接口
- EPPlus / NPOI / Interop 实现

### 视觉模块

- 视觉基础抽象
- 几何模型
- HALCON 集成
- OpenCV 扩展方向
- 测量、标定、扫码、预处理等通用算法基础

### 模板项目

- Shell
- App
- Device
- Flow
- Security
- Vision

## 架构分层

Kwy 以高内聚、低耦合为主要设计原则。每个模块只负责自己的边界，业务项目可以按需引用。

```text
应用模板层
  KwyTemplate.Shell
  KwyTemplate.App
  KwyTemplate.Flow
  KwyTemplate.Device
  KwyTemplate.Security

框架基础层
  Kwy.MVVM
  Kwy.MVVM.WPF
  Kwy.UI.WPF
  Kwy.UI.WPF.Components
  Kwy.Device.Abstractions
  Kwy.Device.Core
  Kwy.Communicate.Abstractions
  Kwy.Communicate.Core

设备与协议扩展层
  Kwy.Device.PLCs.Hsl
  Kwy.Device.MotionCards.Googol
  Kwy.Device.MotionCards.Leadshine
  Kwy.Device.Cameras.HikVision
  Kwy.Communicate.Mqtt
  Kwy.Communicate.OpcUa
  Kwy.Communicate.FMdb
  Kwy.Vision.Halcon
```

## 设备连接模型

模板中的设备连接配置采用集合模型。主配置模型不会因为新增一个设备类型就不断增加属性。

```json
{
  "devices": [
    {
      "deviceId": "PLC.Main",
      "deviceType": "HslPlc",
      "displayName": "主 PLC",
      "enabled": true,
      "connectOnStartup": true,
      "config": {}
    }
  ]
}
```

每一种设备类型通过 `IDeviceConnectionFactory` 提供连接能力。

```text
DeviceConnectionEntry
  -> DeviceType
  -> IDeviceConnectionFactory
  -> 强类型连接配置
  -> 运行时设备实例
  -> IDeviceRegistry
```

这样可以保持模板层稳定。后续客户项目需要增加不同品牌的 PLC、相机、运动控制卡或自定义设备时，只需要新增配置条目和对应工厂，不需要频繁修改主配置模型。

## KwyTemplate 模板

`KwyTemplate` 是一个面向工业设备软件的起始模板，可作为新项目的工程骨架。

```text
KwyTemplate.Shell
  主窗口、标题栏、状态栏和模块承载。

KwyTemplate.App
  主业务 UI、导航和系统配置界面。

KwyTemplate.Device
  设备连接配置、持久化、连接工厂和启动连接。

KwyTemplate.Flow
  机台流程、设备角色和业务流程编排。

KwyTemplate.Security
  本地用户、角色和权限管理。

KwyTemplate.Vision
  视觉流程编辑器和图像检测 UI 基础。
```

## 典型使用场景

- 非标自动化设备软件
- 半导体设备软件
- PLC 设备控制软件
- 视觉检测应用
- 工业数据采集工具
- WPF 工程软件
- 设备 Demo 与项目模板

## 快速开始

克隆仓库后构建解决方案：

```powershell
dotnet build Kwy.slnx
```

运行模板 Shell 项目：

```powershell
dotnet run --project KwyTemplate.Shell/KwyTemplate.Shell.csproj -f net8.0-windows
```

本地生成 NuGet 包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1
```

生成的包会输出到：

```text
artifacts/nuget
```

## NuGet 发布策略

Kwy 推荐以模块化 NuGet 包的方式使用。

推荐用法：

```text
业务项目优先安装功能包。
基础包通常由 NuGet 自动作为依赖解析。
```

例如：

```text
Kwy.Communicate.FMdb
Kwy.Device.PLCs.Hsl
Kwy.UI.WPF.Components
Kwy.Vision.Halcon
```

只有在开发自定义驱动、通信协议、授权器或框架扩展组件时，才建议直接引用基础包。

## 在 Visual Studio 中打包

仓库中提供了一个辅助项目：`Kwy.Packaging`。

普通的 `Release` 解决方案生成不会创建 NuGet 包。打包动作是显式触发的，因此在 Visual Studio 中点击 `生成解决方案` 只会编译项目，不会自动跑完整打包流程。

如需打包发生变化的 Kwy 项目，执行：

```powershell
dotnet build .\Kwy.Packaging\Kwy.Packaging.csproj -c Release -p:RunKwyPackaging=true
```

`Kwy.Packaging` 内部会调用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1 -ChangedOnly -Configuration Release
```

脚本会根据 Git 变更识别需要打包的项目，同时包含依赖它们的上层包；构建解决方案一次后，再逐个执行 `dotnet pack --no-build`，最终输出到：

```text
artifacts/nuget
```

默认情况下，本地流程只生成 NuGet 包，不会推送到远程 NuGet 源。

如果只想预览哪些项目会被打包，可以执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1 -ChangedOnly -DryRun
```

## 设计原则

- 抽象层保持小而稳定。
- 厂商特有配置留在各自厂商模块中。
- 优先使用强类型配置，避免弱类型字典泛滥。
- 使用 `DeviceId` 区分运行时设备实例。
- 设备连接层只负责连接和注册，不承载业务流程。
- 设备能力优先组合，不强行继承成大基类。
- UI 主题资源可替换，方便后续切换主题。
- 每个模块尽量可以独立发布、独立升级。

## 当前状态

Kwy 正在持续开发中。

当前阶段重点是 .NET / WPF 工业应用的工程化落地，尤其关注可维护性、模块化和真实设备接入。

## 许可证

许可证信息后续补充。
