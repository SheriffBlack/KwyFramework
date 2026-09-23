# Kwy.Device 运动控制模块总设计

本文是 `Kwy.Device` 运动控制模块唯一的权威设计文档，适用于精密设备、半导体前道设备、光通信 FAU 多轴耦合机及一般自动化设备。

它定义的是长期目标架构和新增代码必须遵守的边界。某项厂商原生能力尚未由某个具体型号实现时，适配器必须明确报告不支持，不能以 Windows 定时循环降级伪造。

## 1. 设计目标

模块追求四件事：

1. 业务代码只面对稳定业务 ID、工程单位、运动组和工艺语义；
2. 运动控制器承担确定性轨迹、同步和运行期保护；
3. Core 统一设备级准入、配置校验、状态诊断和操作追溯；
4. 厂商差异只存在于设备适配器，不渗透到工艺、UI 或配方。

```text
工艺 / UI / 配方
  -> 业务轴、运动组、空间位姿、MotionProgram
Core 设备级运行时
  -> 配置校验、动作准入、资源仲裁、坐标预检、追溯
厂商控制器适配器
  -> SDK 映射、状态/故障映射、原生程序编译与执行
实时控制器 / PLC Runtime / FPGA / DSP
  -> 插补、前瞻、Jerk、同步、实时保护
安全硬件
  -> STO、急停、安全门、光幕、安全 PLC
```

## 2. 最重要的实时边界

`Core` 通常运行在普通 .NET 进程中，即使设备已连接、即使它正在轮询位置，也不属于实时控制域。

| 域 | 负责内容 | 绝不负责 |
| --- | --- | --- |
| Core 离线预检 | 可达性、静态软限位、已知路径禁入区、配方检查、预计时间 | 控制周期内插补、动态防撞、实时限速。 |
| Core 运行期监控 | UI、诊断、追溯、动作完成等待、非实时报警呈现 | 作为安全停机的唯一依据。 |
| 实时控制器 | 插补、前瞻、Jerk、凸轮齿轮、PSO、跟随误差、扭矩、实时距离保护 | 产品配方、批次追溯、MES/UI。 |
| 安全硬件 | 人身安全链路 | 依赖上位机或普通通信正常运行。 |

倍福 IPC 上的 TwinCAT Runtime、ACS 控制器、以及带真实轨迹内核的 FPGA/DSP 运动卡都可以属于实时域；普通 C# UI、服务或 `Task.Delay` 循环不属于实时域。

## 3. 核心业务模型

| 模型 | 作用 |
| --- | --- |
| `AxisDefinition` | 业务轴 ID、物理 `AxisAddress`、工程单位、轴限位、默认到位、回零和轴级约束。 |
| `MotionGroupDefinition` | 同一控制器内的插补轴顺序、坐标系通道、默认参数和多轴禁入区。 |
| `KinematicMechanismDefinition` | 一套空间机构、关节轴组、机构基座坐标系的绑定。 |
| `CoordinateFrameDefinition` | 机械、工具 TCP、工件、视觉等坐标系及动态版本。 |
| `MotionProgram` | 多段笛卡尔运动意图；不携带物理轴号、SDK 句柄或控制周期点。 |
| `JointTrajectory` | 离线逆解、路径安全和预计时间分析结果；不是通用实时执行格式。 |
| `VirtualAxisDefinition` | 必须由实时控制器维护的虚拟轴资源。 |
| `ElectronicGearDefinition` / `ElectronicCamDefinition` | 由同一实时控制器执行的同步配置。 |

业务层永远使用 `AxisDefinition.Id`、运动组 ID、机构 ID、坐标系 ID 与逻辑 IO 点 ID。`short axis`、坐标系通道和厂商状态字仅限 Core/厂商适配器。

## 3.1 快速入门：文件夹与引用方向

运动模块按“契约、通用运行时、厂商适配器”拆分为独立程序集。文件夹用于让代码容易定位；真正的边界由下列引用方向保证。

```text
设备项目 / 工艺流程 / HMI
        │  只使用业务 ID、工程单位和业务服务接口
        ▼
Kwy.Device.Abstractions
  Motion/
  ├─ Axes/             业务轴、回零可信度、故障、快照
  ├─ Groups/           运动组与直线/圆弧命令
  ├─ Configuration/    配置校验与自动模式门禁契约
  ├─ Operations/       动作追溯与资源锁契约
  ├─ Controller/       控制器原生连续程序能力契约
  ├─ Synchronization/  虚拟轴、电子齿轮、电子凸轮定义
  └─ Kinematics/       位姿、坐标系、机构、离线规划模型
        ▲
        │ 仅引用 Abstractions
        │
Kwy.Device.Core
  Motion/
  ├─ Axes/             业务轴解析、物理轴执行、原点生命周期
  ├─ Groups/           运动组执行、资源仲裁、路径预检
  ├─ Configuration/    启动校验、自动模式就绪门禁
  ├─ Safety/           非实时动作准入守卫
  ├─ Operations/       操作生命周期追溯
  ├─ Synchronization/  同步定义提供者
  └─ Kinematics/
     ├─ Planning/      坐标/逆解/路径的离线预检
     ├─ OfflinePlanning/ 时间估算与雅可比可行性分析
     └─ Controller/    控制器原生程序的业务级编排
        ▲
        │ 引用 Core + Abstractions；实现厂商 SDK 映射
        │
Kwy.Device.MotionCards.*
  ├─ Simulation        仿真与 Core 行为验证
  ├─ Leadshine         雷赛 SDK 适配
  ├─ Googol            固高 SDK 适配
  └─ ZMotion / 其他    按同一规则新增的适配器
        │
        ▼
厂商 SDK / 实时控制器 / 安全控制系统
```

引用规则必须保持单向：

| 程序集 | 可以引用 | 禁止引用 |
| --- | --- | --- |
| `Kwy.Device.Abstractions` | 基础通用库 | `Kwy.Device.Core`、任何厂商卡项目、厂商 SDK。 |
| `Kwy.Device.Core` | `Abstractions` | 任意 `MotionCards.*` 项目、厂商 SDK、工艺项目。 |
| `Kwy.Device.MotionCards.*` | `Abstractions`、`Core`、本厂商 SDK | 其他厂商卡项目、工艺/UI/配方项目。 |
| 设备项目 | 上述稳定业务服务 | 厂商 SDK、物理轴号和原始状态字。 |

新同事建议按下面顺序阅读和使用：

1. 先阅读本文的实时边界、安全策略和三条执行路径；
2. 从 `Axes/AxisDefinition.cs` 建立业务轴、单位、限位、回零和轴级约束；
3. 从 `Groups/MotionGroupModels.cs` 建立插补轴顺序和多轴禁入区；
4. 在项目启动处注册厂商卡、`AddKwyMotionServices(...)`、`AddKwyMotionGroups(...)`，再接入自动模式门禁；
5. 工艺代码仅注入 `IBusinessAxisMotionExecutor`、`IMotionGroupExecutor` 或 `IControllerMotionProgramService`。

`Kinematics/OfflinePlanning` 中的代码仅服务于仿真、配方预检和预计时间；它不是控制器实时轨迹内核。不要因为目录名称相近而把它用于运行期点流控制。

### 业务轴与物理轴通道

轴与 IO、PLC 一样区分“业务定义”和“物理执行”，但运动卡仍直接使用物理轴通道：

```text
IAxisDefinitionProvider
    axisId → AxisDefinition(DeviceId + AxisAddress)
        ↓
IMotionRuntimeRegistry
    DeviceId → 物理运动卡运行时
        ↓
IAxisChannelDefinitionProvider
    short channel → 卡内轴通道配置
        ↓
IMotionCard / IAxisMotionController
```

工艺、配方、HMI 与运动组使用 `axisId`；`short channel` 只允许出现在 Core 物理运行时和厂商适配器中。当前设备级 `AxisDefinitionProvider` 会在运行时组装阶段汇集各张已配置运动卡的轴定义，并一次性校验业务 ID 与物理地址重复；它不会在每次业务运动时遍历所有控制卡。

## 4. 物理能力接口

`IMotionCard` 只表示运动设备生命周期。设备按真实能力实现细粒度接口，而不存在“标准卡”或“高级卡”的大组合接口。

| 接口 | 职责 |
| --- | --- |
| `IAxisMotionController` / `IMotionProfileController` | 物理单轴使能、停止、回零和点位命令。 |
| `IAxisStatusReader` / `IAxisSnapshotReader` | 厂商状态到统一轴快照。 |
| `IMotionWaiter` | 物理停止、到位与回零等待。 |
| `IInterpolationMotionController` | 单段控制器直线/圆弧插补。 |
| `IPositionCompareOutput` | 控制器硬件位置比较输出。 |
| `IMotionControllerCapabilities` | 某型号已验证的原生能力声明。 |
| `IControllerMotionProgramAdapter` | 厂商原生连续程序的编译和执行。 |

单段 `MoveLinear` / `MoveArc` 不代表控制器具备连续轮廓、前瞻、样条、Jerk 或电子凸轮能力。能力只能按照型号、固件、SDK 文档和真机验收结果声明。

## 5. 统一运行时装配

每张物理卡都有一个 `IMotionDeviceRuntime`，并由 `MotionRuntimeFactory` 统一装配：

```text
厂商 MotionCard
  -> MotionStateMonitor
  -> MotionAdmissionGuard
  -> AxisMotionExecutor
  -> MotionDeviceRuntime
```

默认运行时要求设备同时提供单轴控制、带曲线定位和轴快照能力。厂商扩展不得复制这条装配链，只负责设备构造和状态监视配置。

多卡场景必须通过 `IMotionRuntimeRegistry` 按 `DeviceId` 解析运行时，禁止依赖无键注册顺序。

## 6. 动作准入：第一道门，不是实时保护

`IMotionAdmissionGuard` 是所有动作发出前的设备级门禁。它的职责是避免把明显错误的命令交给控制器，不承担动作过程中的实时保护。

统一准入内容：

- 控制器已连接，轴快照存在且未过期；
- 轴未报警、未上报 StopRequired/SafetyCritical 控制器故障、伺服已使能；
- 回零状态和框架回零可信度允许当前动作；
- 当前方向未压限位，目标未超轴级软限位或轴级禁入区；
- 配置的附加互锁规则成立。

资源锁、坐标系版本和多轴路径检查不混入通用准入守卫：

| 专属约束 | 所属服务 |
| --- | --- |
| 单轴资源独占 | `IMotionResourceLock` / 业务轴执行器。 |
| 直线、圆弧、多轴禁入路径 | `MotionGroupExecutor`。 |
| 完整关节轨迹、逆解和程序起点 | `IControllerMotionProgramService`。 |
| 启动期定义合法性 | `IMotionConfigurationValidator`。 |

停止、急停、清报警与失能命令不得被准入门禁拦截。

## 7. 三条执行路径

### 7.1 单业务轴

```text
IBusinessAxisMotionExecutor
  -> 业务轴解析、资源锁、准入、操作追溯
  -> IAxisMotionExecutor
  -> 控制器单轴命令与状态等待
```

适用于点位、相对移动、回零和逻辑传感器寻边。`SettlingTime`、位置容差和速度稳定窗口只用于“动作完成后流程能否继续”的验收，不是运行期保护。

### 7.2 单段运动组插补

```text
IMotionGroupExecutor
  -> 业务轴顺序解析、资源锁、准入、多轴路径预检
  -> IInterpolationMotionController
  -> 控制器单段直线 / 圆弧插补
```

适用于搬运、低复杂度直线和圆弧。Core 不将多段轨迹以定时点流方式模拟为连续轮廓。

### 7.3 控制器原生连续程序

```text
IControllerMotionProgramService.ExecuteAsync(MotionProgram)
  -> 离线逆解、完整路径预检、资源锁、准入、坐标版本检查
  -> IControllerMotionProgramAdapter.CompileAsync(...)
  -> IControllerMotionProgramAdapter.ExecuteAsync(...)
  -> 控制器实时内核执行并返回真实结束结果
```

适用于连续扫描、复杂轮廓、样条、前瞻、S 曲线/Jerk、电子凸轮/齿轮、飞拍和高精度同步。`ExecuteAsync` 完成只能表示控制器程序实际结束，不能只表示已写入缓冲区。

## 8. 状态、故障与回零可信度

`MotionAxisSnapshot` 是 UI、诊断和 Core 的统一状态载体，包含规划/编码器位置、速度、使能、报警、限位、回零状态、原始状态字和 `AxisFault`。

厂商适配器负责将厂商状态字和错误码映射为 `AxisFault`。例如控制器实时检测到跟随误差后，适配器应上报 `AxisFaultCode.FollowingError`；Core 仅将其转换为准入拒绝、动作失败和诊断，绝不轮询规划位置与编码器位置来模拟实时跟随误差保护。

`IAxisHomeLifecycle` 维护框架级原点可信度：

```text
Unknown -> Homing -> Valid
                 -> Failed
Valid -> Invalidated
```

控制器重连、伺服失能、清报警、外部移动和软件重启会使可信度失效。绝对定位与自动程序只接受 `Valid`。

## 9. 坐标、机构与离线规划

Core 可以进行静态坐标变换、TCP/工件偏移、逆解候选选择、关节限位、奇异性预警、路径禁入区预检和节拍估算。

`AddKwyOfflineMotionPlanning()` 注册的时间参数化、雅可比速度分析只用于仿真、配方预检、预计时间和风险提示；它们不向控制器周期性下发速度或位置。

| 能力 | Core 可做 | 必须由实时域做 |
| --- | --- | --- |
| TCP、静态标定、工件偏移 | 编译前变换和预检 | 控制周期中的在线补偿。 |
| 逆解、可达性 | 候选解、关节限位、奇异性检查 | 在线跟踪和动态重规划。 |
| 雅可比限速 | 配方速度可行性分析 | 随姿态变化的周期性限速。 |
| 禁入区、相对距离 | 目标和已知路径检查 | 实际位置的实时防撞/最小距离保护。 |
| 视觉补偿 | 更新下一动作的坐标系 | 连续视觉闭环跟踪。 |
| 位置触发 | 配置业务规则 | PSO、比较输出、采集同步。 |

## 10. 同步、虚拟轴与硬件触发

电子齿轮、电子凸轮、虚拟轴和位置比较输出必须由控制器原生能力执行：

- 虚拟轴必须绑定声明 `SupportsControllerVirtualAxis` 的同一实时控制器；
- 电子齿轮/凸轮的主从轴必须承载于同一控制器，并分别声明原生能力；
- `IPositionCompareOutput` 只配置控制器硬件比较规则，不能用 Core 轮询位置再写 DO 替代；
- Core 负责配方、业务 ID、能力校验、下载/启动和追溯，不进行软件主从同步。

## 11. 安全策略、停止与抱闸

本模块参与设备风险降低，但不是人身安全控制系统。设备安全方案必须先完成风险评估，并形成独立、受版本控制的安全功能规范；运动配置只能引用已批准的安全功能，不能自行定义安全等级。

### 11.1 安全职责与不可替代关系

| 层级 | 可以承担的职责 | 不得作为唯一手段 |
| --- | --- | --- |
| 固有安全设计 | 限制质量、速度、行程、夹点和可接近性 | 不能用软件限制替代机械防护。 |
| 防护装置与安全控制系统 | 急停、安全门、光幕、双手按钮、安全速度/安全停止、STO 等安全功能 | 不依赖 Windows、普通以太网、普通 DI 或应用进程。 |
| 实时运动控制器 | 控制器已认证或经安全设计纳入的硬限位、跟随误差、扭矩、实时距离与安全运动功能 | 不假定普通状态字或普通 DO 已具有人身安全等级。 |
| Core 准入与诊断 | 防错、配置校验、工艺联锁、报警呈现、追溯和受控恢复 | 不得宣称为急停、STO、光幕或实时防撞回路。 |

`IMotionAdmissionGuard`、禁入区预检、回零可信度和 IO 互锁属于“减少错误命令”的第一道门。它们失效、延迟或未运行时，安全硬件与实时安全功能仍必须将残余风险控制在风险评估允许范围内。

### 11.2 安全功能规格必须独立维护

每个项目应在设备安全模块或项目安全文档中维护安全功能清单，而不是把 PL/SIL、急停逻辑或安全输入散落在 `AxisDefinition`、工艺步骤和 UI 中。每项至少包含：

- 稳定安全功能 ID、危险源和风险评估条目；
- 触发源、输入诊断方式、最终执行元件和安全状态；
- 适用模式（自动、手动、维护、调试）、复位条件和人工确认要求；
- 要求的性能等级或安全完整性目标，以及对应的验证证据；
- 与运动资源、门锁、区域、抱闸、STO、安全速度或安全停止的关联；
- 安全功能版本、责任人、变更原因和重新验证记录。

安全功能的性能目标应由项目风险评估确定。机械风险评估与风险降低方法可参考 [ISO 12100](https://www.iso.org/standard/51528.html)；安全相关控制系统的设计与集成可依据项目适用法规选择 [ISO 13849-1:2023](https://www.iso.org/standard/73481.html) 或 [IEC 62061](https://webstore.iec.ch/en/publication/59927)。框架本身不计算或宣称 PL、SIL、类别或合规结论。

### 11.3 停止语义

最终设计中的停止等级必须明确区分：

| 等级 | 语义 | 执行归属 |
| --- | --- | --- |
| `ControlledStop` | 按控制器减速度受控停止 | 控制器。 |
| `ImmediateStop` | 控制器立即停止/急减速 | 控制器。 |
| `ControllerSafetyStop` | 控制器配置的硬件快速输入或安全停轴 | 控制器/安全接口。 |
| `EmergencyStop` | 协调状态与记录，不替代硬件急停回路 | 安全硬件优先。 |

`EmergencyStop` 在 Core 中只能表达“已检测到安全链路动作、停止业务流程并记录”的状态，不得把普通 `Abort()`、停止命令或 DO 写入命名为急停。安全回路的复位必须由安全控制系统完成；Core 只能在安全回路已确认恢复后，重新执行设备状态同步、回零可信度检查和自动模式门禁。

垂直轴必须通过 `IAxisBrakeCoordinator` 管理“伺服稳定 → 释放抱闸 → 运动 → 停稳 → 合闸 → 可选失能”的顺序和超时。抱闸的失电/得电安全状态、制动力、保持时间和 STO 协同必须来自机械及电气设计；任何上位机逻辑都不能绕过抱闸策略。

### 11.4 模式、权限与恢复

- 自动模式只允许经 `IMotionAutoModeGate` 校验后执行受配方约束的业务动作；
- 手动、维护和工程模式必须由设备模式管理与物理选择装置共同限制，不以 UI 隐藏按钮代替权限边界；
- 维护点动应受独立速度、使能、区域和死手/确认策略约束，不能复用自动配方的全速动作；
- 安全触发、控制器重连、伺服失能、清报警或检测到外部移动后，相关轴的 `HomeValidity` 必须失效；
- 清报警不等于允许恢复运动。恢复顺序应为：安全回路恢复并确认 → 故障原因排除 → 控制器状态同步 → 回零/位置确认 → 自动模式门禁 → 人工授权恢复。

安全硬件、STO、安全 PLC、硬限位、控制器跟随误差和控制器扭矩保护始终是最终保护层；Core 准入只能减少错误命令，不能替代它们。

## 12. 启动与自动模式门禁

控制器连接完成及设备进入自动模式前必须运行 `IMotionConfigurationValidator` / `IMotionAutoModeGate`。至少校验：

- 业务轴 ID、物理卡/通道、运动组 ID、坐标系通道唯一；
- 运动组成员属于同一卡，成员顺序与机构关节顺序一致；
- 轴限位、默认参数、回零、抱闸、禁入区和互锁定义合法；
- 引用的 IO 点、控制器、虚拟轴、凸轮、齿轮均存在；
- 同步关系绑定同一控制器且控制器声明对应原生能力；
- 配置错误时禁止自动模式，而不是运行到该动作才报错。

调用 `AddKwyMotionGroups(...)` 后，`IMotionAutoModeGate` 会进入标准设备流程：`EquipmentModeService` 切换到 `Auto`、`DryRun` 或 `Production` 时先校验；`EquipmentProcessController` 的初始化、启动和恢复运行前也会再次校验。标准流程会先确认控制器在线并启动每张卡的状态监视器；`StartAsync` 成功返回前必须完成首帧快照采集。若校验失败，设备不会进入 `Ready` 或 `Running`，并转入需要人工处理的状态。项目若绕过这些标准入口，必须在自己的等价入口显式调用该门禁。

## 13. 厂商适配器规范

厂商项目只做以下工作：

1. 打开/关闭 SDK 资源和设备生命周期；
2. 将厂商原始状态、错误码和等待语义映射为 Kwy 抽象；
3. 如实实现硬件实际具备的能力接口；
4. 对支持连续程序的型号，实现 `IControllerMotionProgramAdapter`；
5. 提供厂商 SDK 契约测试和真机验收测试。

厂商项目不得包含工艺流程、业务点位、产品配方、跨设备流程、Windows 定时插补或软件实时保护循环。

新型号启用高级能力前至少验证：SDK/API 文档、真机程序执行、缓冲欠载/超载语义、停止/故障语义、控制周期与同步触发，以及安全验收。未验证时只暴露已确认的较低层能力。

## 14. 工艺层使用规则

工艺层优先依赖：

```text
IBusinessAxisMotionExecutor
IMotionGroupExecutor
IPoseMotionExecutor
IControllerMotionProgramService
IMotionOperationTracker
IMotionAutoModeGate
```

工艺层不得直接注入 `IMotionCard`、`IAxisMotionController`、`IInterpolationMotionController`，不得出现物理轴号、脉冲换算、厂商 SDK 类型或原始状态字。

每个单轴、运动组、回零、寻边和原生程序动作都必须通过 `IMotionOperationTracker` 记录操作 ID、资源、目标、开始时间、结束状态、取消原因和故障。

## 15. 测试与验收分层

| 层级 | 覆盖重点 |
| --- | --- |
| 模型测试 | 配置合法性、单位、限位、禁入区、坐标变换、逆解候选。 |
| Core 测试 | 准入、资源互斥、回零可信度、动作生命周期、路径预检。 |
| 厂商契约测试 | SDK 调用映射、状态/故障映射、停止、回零、插补能力。 |
| 控制器程序集成测试 | 原生轨迹、前瞻、PSO、凸轮/齿轮和同步语义。 |
| 真机安全验收 | 急停、STO、限位、抱闸、跟随误差、扭矩、相对距离。 |

仿真卡复用 `AxisDefinition`、Core 准入和运行时装配，但不得伪造原生连续轨迹、实时防撞或硬件同步能力。

安全验收除“触发后是否停止”外，还必须验证安全状态、停止时间/距离、复位不可自动恢复、单故障行为、断线/掉电行为、模式切换、抱闸保持、诊断覆盖及安全功能追溯。任何安全相关硬件、参数、固件、轴负载、机械防护或安全逻辑变更，均须回到项目风险评估并执行对应的重新验证。

## 16. 新功能设计检查表

新增运动功能前必须回答：

1. 它是业务语义、Core 预检、厂商适配，还是实时控制器功能？
2. 若在运行中每周期执行，是否已下沉到实时控制器？
3. 是否仅依赖业务 ID 与工程单位，避免物理通道泄漏？
4. 是否声明真实硬件能力，并在能力缺失时明确拒绝？
5. 是否经过统一准入、资源锁、操作追溯和自动模式门禁？
6. 是否把人身安全留给安全硬件，而未误称 Core 为安全回路？

若任何答案不清晰，不应直接向模块新增接口或实现；先明确所在层和实时边界。
