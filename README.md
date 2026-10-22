# KwyFramework

KwyFramework 是一套面向工业设备软件的模块化 .NET 框架，用于构建设备控制、非标自动化、半导体设备、机器视觉和 WPF 工程应用。

框架将通信、设备、数据、MVVM、UI、日志、文件、授权和视觉等通用能力拆分为可按需引用的模块，并提供可直接扩展的工业应用模板。

> 仓库正在持续开发中。各模块的 API、依赖、配置和示例以其目录中的 `README.md` 为准。

## 框架组成

| 领域 | 项目 | 说明 |
|---|---|---|
| 基础能力 | `Kwy.Core`、`Kwy.ComponentModel` | 通用工具、线程与元数据能力 |
| 通信 | `Kwy.Communicate.*` | TCP/Serial、MQTT、OPC UA、SECS/GEM、GPIB 等通信能力 |
| 设备 | `Kwy.Device.*` | PLC、IO 卡、运动控制、相机和仪表的抽象与实现 |
| 数据 | `Kwy.Data.*` | 数据访问抽象、SQL 和 EF Core 实现 |
| MVVM | `Kwy.MVVM`、`Kwy.MVVM.WPF` | 消息、命令、模块化、导航、对话框和权限 |
| WPF UI | `Kwy.UI.*` | 主题、控件、属性编辑、图表和流程设计器 |
| 日志 | `Kwy.Logging.Serilog` | 基于 `Microsoft.Extensions.Logging` 的 Serilog 文件日志集成 |
| 文件 | `Kwy.Files.*` | JSON、INI 和 Excel 文件处理 |
| 授权 | `Kwy.Licensing.*` | 软件授权与商业加密设备抽象 |
| 视觉 | `Kwy.Vision.*` | 视觉抽象、几何模型和 HALCON 等实现 |
| 应用模板 | `KwyTemplate.*` | Shell、App、Device、Flow、Security、MES 和 Vision 示例结构 |

各项目遵循“抽象与实现分离”的组织方式。通用代码优先依赖抽象包，最终应用再选择具体的设备、通信、数据库、日志或视觉实现。

## 文档导航

首次了解整体设计，请阅读 [KwyFramework 概览](docs/KwyFramework-Overview.zh-CN.md)。

常用模块文档：

- [MVVM 核心](Kwy.MVVM/README.md)
- [MVVM WPF 集成](Kwy.MVVM.WPF/README.md)
- [WPF UI](Kwy.UI.WPF/README.md)
- [WPF 组件](Kwy.UI.WPF.Components/README.md)
- [FlowDesigner](Kwy.UI.WPF.FlowDesigner/README.md)
- [通信抽象](Kwy.Communicate.Abstractions/README.md)
- [设备核心](Kwy.Device.Core/README.md)
- [数据访问抽象](Kwy.Data.Abstractions/README.md)
- [文件处理](Kwy.Files/README.md)
- [Serilog 日志](Kwy.Logging.Serilog/README.md)
- [视觉抽象](Kwy.Vision.Abstractions/README.md)
- [模板设备层](KwyTemplate.Device/README.md)
- [模板流程层](KwyTemplate.Flow/README.md)
- [模板视觉层](KwyTemplate.Vision/README.md)

其余模块请直接查看对应项目目录中的 `README.md`。

## 快速开始

需要 .NET 8 SDK。在仓库根目录执行：

```powershell
dotnet build Kwy.slnx
```

运行 WPF 模板应用：

```powershell
dotnet run --project KwyTemplate.Shell/KwyTemplate.Shell.csproj -f net8.0-windows
```

项目可以通过 `ProjectReference` 在源码树内使用，也可以以 NuGet 包形式按需引用。各包的安装和配置方式请查看其模块文档。

## 设计方向

- 保持模块边界清晰，避免业务层依赖具体厂商实现。
- 优先使用强类型配置和组合式设备能力。
- 设备连接、运行时管理和业务流程分层实现。
- 保持基础模块可独立测试、打包和升级。
- 优先采用 .NET 生态的标准抽象，减少重复封装。

## 许可证

本仓库采用按项目分范围授权，并非所有内容均以开源许可证发布。

当前明确采用 [MIT License](LICENSES/MIT.txt) 的项目范围，请参阅 [LICENSE.md](LICENSE.md) 和 [LICENSES/README.md](LICENSES/README.md)。

Copyright © 2026 Kwy。
