# Kwy 框架介绍

Kwy 是一套面向工业设备软件的模块化 .NET 框架。它不是单一的大型业务系统，也不要求项目一次引用全部功能；它提供的是一组可按需组合的基础能力，用于构建设备控制软件、自动化产线软件、测试与测量软件、机器视觉工具，以及基于 WPF 的工程桌面应用。

如果把一个工业软件项目比作一台机器，Kwy 提供的不是某个工位的业务流程，而是通信线缆、设备接口、控制面板、运行状态、日志、权限、配置、视觉能力和项目骨架。

## 它要解决什么问题

工业软件的复杂性通常不在某一条 PLC 指令或某一台相机，而在这些能力长期叠加后产生的耦合：

- 通信协议、设备对象和业务流程混在一起，替换厂商或协议时影响范围很大。
- 界面逻辑直接操作设备，难以测试，也难以复用。
- 配置、连接、状态、异常恢复各项目重复实现，行为不一致。
- 新项目从窗口、导航、权限、日志、设备启动重新搭建，交付速度慢。
- 某个项目写出的“通用组件”实际混入了具体机台、OK/NG 判定或客户业务语义。

Kwy 的目标是把这些共性能力拆开，形成稳定边界：业务只表达“这台机要做什么”，框架和扩展模块负责“如何连接、如何显示、如何管理生命周期”。

## 一句话定位

> Kwy 是一套以 .NET 和 WPF 为主要技术栈、面向工业设备集成的模块化框架与项目模板。

它适合需要同时面对设备连接、交互界面、配置管理、流程编排和工程化交付的桌面应用。

## 适用场景

- 非标自动化设备与上位机软件。
- 半导体、测试分选、包装、检测等设备软件。
- PLC、IO、运动卡、仪器、相机等多设备协同控制。
- 机器视觉检测、图像采集、标定与算法流程工具。
- 需要统一主题、权限、导航、日志和配置管理的 WPF 工程应用。
- 希望从模板快速启动、再逐步替换为项目专属业务模块的团队。

Kwy 不试图替代完整的 MES、ERP 或云端平台；它更关注设备侧和工程桌面端的应用基础设施。

## 整体架构

```text
┌──────────────────────────────────────────────────────┐
│                    项目业务层                         │
│  机台流程 / 产品工艺 / 客户需求 / 页面与业务规则       │
├──────────────────────────────────────────────────────┤
│                  KwyTemplate 模板层                   │
│ Shell / App / Device / Flow / Security / Vision       │
├──────────────────────────────────────────────────────┤
│                    Kwy 框架基础层                     │
│ MVVM / UI.WPF / Device.Core / Communicate.Core        │
│ Files / Logging / Licensing / Vision.Abstractions     │
├──────────────────────────────────────────────────────┤
│                  协议与厂商适配层                     │
│ TCP / Serial / MQTT / OPC UA / Modbus / PLC / 相机    │
│ 运动卡 / 仪器 / HALCON / OpenCV / 第三方 SDK          │
└──────────────────────────────────────────────────────┘
```

依赖方向应尽量从上到下：业务层依赖抽象和功能包，功能包依赖核心抽象；核心抽象不反向依赖具体厂商或某个机台项目。

## 核心模块

| 模块族 | 主要职责 | 适合何时引用 |
|---|---|---|
| `Kwy.MVVM` | 命令、消息、模块、导航、权限等 UI 无关能力 | 所有采用 Kwy MVVM 的应用 |
| `Kwy.MVVM.WPF` | WPF 应用启动、区域导航、Dialog、ViewModel 定位 | WPF + Kwy MVVM 应用 |
| `Kwy.UI` | UI 无关的轻量契约 | 被 WPF UI 或业务层间接使用 |
| `Kwy.UI.WPF` | 主题、样式、控件、行为、文件对话框等 | 需要 Kwy WPF 基础 UI |
| `Kwy.UI.WPF.Components` | 更高层的 WPF 组件与交互能力 | 需要现成 Dialog、属性编辑等组件 |
| `Kwy.Communicate.*` | 通信抽象与 TCP、串口、MQTT、OPC UA、Modbus 等实现 | 按实际协议选择 |
| `Kwy.Device.*` | 设备抽象、注册、生命周期、PLC、IO、相机、运动、仪器等 | 需要设备接入与运行时管理 |
| `Kwy.Files.*` | JSON、INI、Excel 等文件能力 | 按文件格式选择实现包 |
| `Kwy.Logging.*` | 日志抽象与 Serilog 集成 | 需要统一日志时 |
| `Kwy.Licensing.*` | 授权与加密狗等能力抽象 | 有商业授权需求时 |
| `Kwy.Vision.*` | 视觉抽象、图像源、算法与 HALCON/OpenCV 等扩展 | 需要视觉能力时 |
| `KwyTemplate.*` | 可运行的工业应用工程骨架 | 新项目启动或参考实现 |

## 模块化，而不是“一次装全家桶”

一个只需要 WPF 界面和 MQTT 的工具，通常只需要引用对应的 MVVM、UI 与通信包；它不需要相机、运动卡或 HALCON。

一个设备项目也应优先引用功能包，而不是直接依赖所有基础项目：

```text
需要 Modbus        → Kwy.Communicate.FMdb
需要 TCP / 串口    → Kwy.Communicate.TcpSerial
需要 HSL PLC       → Kwy.Device.PLCs.Hsl
需要 Kwy WPF UI    → Kwy.UI.WPF
需要视觉算法       → Kwy.Vision.* 对应实现包
```

基础包会作为依赖自动解析。这样可以减少最终应用的依赖体积，也让协议和厂商 SDK 保持可替换。

## 通信与设备：配置驱动的创建方式

Kwy 的一个重要思想是：调用方只描述配置，不直接判断并创建具体通信或设备类型。

例如通信工厂会把“配置类型”映射为“通信客户端创建方式”：

```text
TcpConfig          → TcpCommunication
SerialPortConfig   → SerialPortCommunication
MdbConfig          → FMdbCommunication
OpcUaConfig        → OpcUaCommunication
```

注册发生在对应协议模块中：

```csharp
var factory = new CommunicationFactory();
factory.RegisterTcpSerialClients();
factory.RegisterFluentModbus();

ICommunicationClient client = factory.CreateClient(protocolConfig);
```

业务代码只面向 `ICommunicationClient`，不需要依赖 `TcpCommunication` 或 `SerialPortCommunication`。

这带来三个直接收益：

- 新增协议时，以扩展注册方式接入，不修改核心工厂的 `if/else`。
- 设备可以通过同一份 `IProtocolConfig` 获取通信能力。
- 测试时可替换为模拟客户端或模拟设备。

设备工厂采用相同思路：

```text
设备配置 → IDeviceFactory → 具体设备实例 → IDeviceRegistry
```

运行时通过稳定的 `DeviceId` 找设备，而不是让业务层到处保存具体设备对象。

## MVVM 与 WPF UI 的分工

Kwy 将界面实现分为两层：

```text
Kwy.MVVM
  不引用 WPF，负责命令、消息、模块、导航和权限抽象。

Kwy.MVVM.WPF
  将 MVVM 抽象接入 WPF，负责应用启动、区域导航、Dialog 等。

Kwy.UI.WPF
  提供主题、样式、控件、行为和 WPF 平台服务。
```

这样 ViewModel 尽量不直接引用具体控件类型，界面主题和设备业务也不会互相绑死。

对于 UI 库，Kwy 的边界是“通用 WPF 能力”。例如主题、数值输入、Toast、文件拖放、复制文本、自动滚动等适合放在 `Kwy.UI.WPF`；某个机台的 OK/NG、料盘、工站、产品参数等则应留在业务项目或独立行业扩展中。

## KwyTemplate：从工程骨架开始

`KwyTemplate` 不是必须使用的运行时依赖，而是一套可参考、可裁剪的工业软件骨架：

```text
KwyTemplate.Shell      主窗口、标题栏、状态栏、模块承载
KwyTemplate.App        主业务界面、导航与系统配置
KwyTemplate.Device     设备配置、连接初始化、设备目录
KwyTemplate.Flow       机台流程、设备角色与编排
KwyTemplate.Security   本地用户、角色和权限
KwyTemplate.Vision     视觉流程编辑与图像检测 UI
```

新项目的推荐做法不是永久修改模板本身，而是：

1. 以模板作为启动工程。
2. 新建项目自己的业务模块。
3. 在业务模块中定义设备目录、页面、流程和工艺规则。
4. 只在确实具有跨项目复用价值时，才把能力下沉到 Kwy 包。

## 推荐采用路径

### 只需要轻量 MVVM

```text
Kwy.MVVM
Kwy.MVVM.WPF（仅 WPF 项目）
```

适合已有 UI、只希望使用模块化、导航、消息或命令基础设施的项目。

### 新建 WPF 工程应用

```text
Kwy.MVVM.WPF
Kwy.UI.WPF
Kwy.Logging.*
Kwy.Files.*
```

适合带统一界面风格、配置和日志需求的桌面工具。

### 新建设备控制软件

```text
KwyTemplate.Shell / App / Device
Kwy.Communicate.*
Kwy.Device.*
Kwy.Logging.*
```

按实际设备逐步加入 PLC、相机、运动卡、仪器和视觉实现包。

### 新建视觉检测应用

```text
KwyTemplate.Vision
Kwy.Vision.Abstractions
Kwy.Vision.Halcon 或 Kwy.Vision.OpenCV
```

视觉算法、模型和相机能力应按实现包选择，不应让基础 UI 或 MVVM 包承载厂商 SDK。

## 设计原则

- **抽象优先**：业务依赖接口和配置，不直接依赖厂商 SDK。
- **按需组合**：协议、设备、UI 和视觉能力通过功能包选择。
- **高内聚、低耦合**：通用 UI 不混入机台和判定业务；厂商实现不进入核心抽象。
- **显式配置**：优先强类型配置与明确注册，少依赖字符串猜测和反射约定。
- **可测试性**：通信、设备、日志等能力经由接口替换，方便模拟测试。
- **模板与框架分离**：模板提供参考实现，框架沉淀跨项目稳定能力。
- **渐进式演进**：先在真实项目验证，再决定是否升级为公开 NuGet API。

## 常见问题

### Kwy 是否要求使用全部模块？

不要求。模块设计的目的就是按需引用。一个纯 WPF 工具不应被迫引入 PLC、相机或视觉 SDK。

### Kwy 是否等同于某个具体设备项目？

不等同。机台流程、产品规格、OK/NG 判定、客户专属协议等属于业务层；Kwy 提供可复用基础设施与扩展机制。

### 为什么通信和设备要使用工厂？

因为协议和厂商实现会持续增加。工厂使“配置类型到具体实现”的选择集中在注册处，业务侧只依赖抽象接口，避免不断扩大的条件判断。

### 为什么部分功能暂不作为公开 NuGet 包？

依赖第三方 SDK 或只有单一项目验证过的适配层，过早公开会形成难以维护的兼容承诺。Kwy 倾向先稳定核心包，再按真实复用需求发布适配包。

## 许可证与使用范围

仓库并非所有项目都自动采用同一许可证。

当前已明确以 MIT 开源发布的范围以根目录 [README](../README.md#许可证) 和 [LICENSE-MVVM.md](../LICENSE-MVVM.md) 为准，主要包括 `Kwy.MVVM` 与 `Kwy.MVVM.WPF` 相关项目。

其他项目、厂商适配和模板代码是否可使用、修改或再分发，必须以其目录内的许可证或后续明确声明为准。使用前请先确认对应模块的授权边界。

## 下一步

- 阅读根目录 [README](../README.md) 了解模块清单、构建和本地打包方式。
- 从需要的模块目录开始阅读其 `README.md`。
- 需要直接启动应用时，从 `KwyTemplate.Shell` 与 `KwyTemplate.Device` 的结构开始。
- 需要扩展协议或设备时，参考通信与设备模块中的 `*FactoryExtensions` 注册方式。
