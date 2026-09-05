# 离线 MES 工单与本地配方设计

> 适用范围：`Machine_2_A`、`Machine_4_HAHH`，以及后续接入的其他机型。  
> 当前客户适配：Cyntec。  
> 文档状态：第一阶段已实现本地配方创建、加载、仪表参数回写；“工单功能参数编辑 UI”待后续补充。

## 1. 背景与目标

现场并非始终可以连接 MES。断线时仍需要能够：

1. 按工单维护 DCR、Ls、Rs、频率、量程、包装参数等生产参数；
2. 将参数写入对应仪表和 PLC；
3. 下次加载同一工单时恢复相同参数，不需要重新逐项设置；
4. 不把 Cyntec 的外部字段名、TXT 格式或某个机型的仪表协议扩散到 UI 和业务层；
5. 仪表通讯失败时，不能丢失工程师已经输入的参数。

本设计将客户 MES、机型能力、离线工单配方和运行时执行状态解耦，以轻量 ISA-88 思路组织，而不引入完整批处理平台的复杂度。

## 2. 核心原则

### 2.1 工单不等于机型

同一个 Cyntec 工单可以包含 DCR、Ls、Rs、极性、Q 等完整参数；不同机型按自身能力消费其中的一部分。

```text
同一份统一工单配方
 ├─ Machine_2_A    → DCR1、DCR2
 └─ Machine_4_HAHH → DCR1、Ls、Rs、极性、Q（按工单开关）
```

因此禁止使用“一个机型一套 Cyntec 工单格式”或“一个机型一个解析器”的设计。

### 2.2 客户字段不得进入机型与 UI

例如 Cyntec 外部字段：

```text
ZEnable、QEnable、LEnable2、LMinValue、RSMaxValue、LFreq
```

只能存在于 `KwyTemplate.MES.Cyntec` 的解析适配层。内部统一使用稳定参数 ID：

```text
Polarity.Enabled
Q.Enabled
Inductance.SecondFrequency.Enabled
Ls
Rs
DCR1
DCR2
Z1 / PHASE1
Z2 / PHASE2
```

这样客户 B 即使把极性字段定义为 `NeedPolarity=1`，也只需增加客户适配映射，不影响 SetView、Flow 或仪表驱动。

### 2.3 配方与设备能力分离

| 信息 | 所属位置 | 是否随工单变化 |
|---|---|---|
| 仪表型号、通讯口、PLC 点位、工位物理存在 | `Machine_*_DeviceCatalog` | 否 |
| DCR/Ls/Rs 上下限、频率、单位、量程 | `{工单}.json` | 是 |
| 极性/Q/第二频是否需要测试 | `{工单}.json` | 是 |
| PLC/HMI 当前人工工位开关 | StationView / PLC | 可临时变化 |
| 当前卷实际使用的参数、生产结果 | 运行内存、生产记录、MES | 是 |

`StationView` 仅表达 PLC/HMI 的人工工位状态，不能用来保存 `ZEnable` 等工单工艺要求。

## 3. 分层结构

```text
┌──────────────────────────────────────────────────────────────┐
│ 客户适配层：KwyTemplate.MES.Cyntec                            │
│ 工单.txt / 工单_3.txt / MES API → MesWorkOrderSetup           │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│ 统一工单模型：KwyTemplate.MES.Abstract                         │
│ MesWorkOrderSetup / MesWorkOrderInstrumentSetup               │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│ 本地配方层：KwyTemplate.App                                    │
│ LocalWorkOrderRecipe ↔ LocalWorkOrderRecipeMapper             │
│ LocalWorkOrderRecipeStore / LocalWorkOrderRecipeSession       │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│ 机型执行层：KwyTemplate.Flow                                  │
│ Machine_2_A / Machine_4_HAHH.ApplyWorkOrderSetupAsync         │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│ 设备层：Kwy.Device.Instruments / PLC / IO                      │
│ 仪表协议、单位转换、实际通讯                                   │
└──────────────────────────────────────────────────────────────┘
```

依赖方向必须保持从上到下：

```text
App 可以引用 MES.Abstract、Flow、Device
MES.Cyntec 只能引用 MES.Abstract
Flow 不引用 MES.Cyntec，也不引用 WPF/App
Device 不引用 Flow、App、MES
```

## 4. ISA-88 的轻量映射

| ISA-88 概念 | 当前项目映射 |
|---|---|
| Control Module | 单个 DCR、Hioki LCR、PLC 点位、IO 点位 |
| Equipment Module | 一个测试工位及其仪表、握手点位、数据处理 |
| Unit | `Machine_2_A`、`Machine_4_HAHH` 一台机台 |
| Equipment Capability | `Machine_*_DeviceCatalog` 和 `TestStationModel` |
| Master Recipe | 本地 `{工单}.json` |
| Control Recipe | 当前已加载并实际下发的 `MesWorkOrderSetup` |
| Batch Record | 生产记录、点检 TXT、MES 进出站、日志 |

完整 ISA-88 还包含排产、资源分配、批次过程控制等内容；本项目当前只采用其中“设备能力与配方分离”的部分。

## 5. 本地配方文件

### 5.1 文件位置

运行目录默认位置：

```text
Config/ParameterDict/Files/{工单号}.json
```

目录及允许后缀由：

```text
Config/ParameterDict/ParameterDictOptions.json
```

配置。参数字典页面可以展示 `.json` 和 `.txt`；但**可作为本地工单配方加载的只有 `.json`**。`.txt` 用于兼容客户来源文件或人工参考，不作为程序自己的配方格式。

### 5.2 JSON 结构

新建 JSON 会生成版本化配方骨架，示例：

```json
{
  "schemaVersion": 1,
  "workOrderNo": "0002595",
  "machineProfileKey": "Machine_4_HAHH",
  "source": "Local",
  "equipmentType": "CTC-X",
  "parameters": {
    "Polarity.Enabled": "true",
    "Q.Enabled": "false",
    "Inductance.SecondFrequency.Enabled": "false",
    "Ls.Frequency": "5",
    "Ls.FrequencyUnit": "MHz"
  },
  "measurements": [
    {
      "parameterId": "DCR1",
      "displayName": "DCR1",
      "enabled": true,
      "lowerLimit": 79,
      "upperLimit": 98,
      "unit": "mΩ",
      "range": "1Ω"
    },
    {
      "parameterId": "Ls",
      "displayName": "Ls",
      "enabled": true,
      "lowerLimit": 0.825,
      "upperLimit": 1.18,
      "unit": "μH",
      "range": "300Ω"
    },
    {
      "parameterId": "Rs",
      "displayName": "Rs",
      "enabled": true,
      "lowerLimit": 930,
      "upperLimit": 2500,
      "unit": "mΩ",
      "range": "300Ω"
    }
  ],
  "materialRequirements": {
    "tablePaperMatNo": "3504116400",
    "topCoverMatNo": "3506507600",
    "reelMatNo": null
  },
  "tapeSetup": {
    "beforeSpaceQty": 105,
    "packageQty": 4003,
    "afterSpaceQty": 105,
    "sampleQty": 1,
    "blankQty": 6,
    "backNoFilmQty": null
  }
}
```

### 5.3 字段说明

| 字段 | 含义 |
|---|---|
| `schemaVersion` | 配方格式版本，用于未来升级兼容 |
| `workOrderNo` | 工单主键，同时决定文件名 |
| `machineProfileKey` | 创建/最后编辑时的机型追溯信息，不限制工单只能用于该机型 |
| `source` | `Local` 或 `MES`，仅记录来源 |
| `equipmentType` | MES 返回的机种型号；X 机种逻辑依赖此值 |
| `parameters` | 工艺功能开关、频率等非单一测量上下限参数 |
| `measurements` | 各测量项上下限、单位、量程 |
| `materialRequirements` | 台纸、上盖、Reel 等物料要求 |
| `tapeSetup` | 包装、空格、样品等编带参数 |

## 6. Cyntec 适配

### 6.1 Cyntec 的职责

`KwyTemplate.MES.Cyntec.CyntecMesFileParser` 只负责：

```text
Cyntec 工单.txt / 工单_3.txt
→ MesParameterBag + MesWorkOrderInstrumentSetup
→ MesWorkOrderSetup
```

它不应引用：

```text
WPF 控件
SetViewModel
Machine_2_A / Machine_4_HAHH
LocalWorkOrderRecipe
```

### 6.2 当前字段映射

| Cyntec 字段 | 统一内部语义 |
|---|---|
| `DCRMinValue` / `DCRMaxValue` | `DCR1` 上下限 |
| `DCRMinValue2` / `DCRMaxValue2` | `DCR2` 上下限 |
| `LMinValue` / `LMaxValue` | `Ls` 上下限 |
| `RSMinValue` / `RSMaxValue` | `Rs` 上下限 |
| `LUnit` / `RSUnit` | Ls / Rs 单位 |
| `LFreq` | Ls/Rs 频率 |
| `LCRRange` | LCR 量程 |
| `ZEnable` | `Polarity.Enabled` |
| `QEnable` | `Q.Enabled` |
| `LEnable2` | `Inductance.SecondFrequency.Enabled` |

> 注意：`LEnable2` 仅控制第二套频率参数，不能错误地关闭主频 Ls/Rs。

### 6.3 多机型消费规则

```text
Machine_2_A
  消费 DCR1、DCR2
  忽略 Ls、Rs、Polarity、Q

Machine_4_HAHH
  消费 DCR1、Ls、Rs
  Polarity.Enabled=true 时消费 Z1/PHASE1、Z2/PHASE2
  Q.Enabled=true 时消费 Q
```

工单提供了机型不支持的参数时不报错，直接忽略；机型必需参数缺失时，应在启动前给出明确提示。

## 7. 参数字典与界面职责

### 7.1 ParameterDictView

负责文件级操作：

```text
新建 → 创建 {工单}.json 配方骨架
选择 → 筛选、选中本地文件
导入 → 仅离线时加载 JSON 并发送给 HomeView
删除 → 确认后删除选中的文件
```

它不编辑每个仪表字段，也不直接写仪表。

### 7.2 SetView

负责参数级操作：

```text
工位 DCR / Hioki 页面
→ 修改上下限、单位、频率、量程
→ 点击应用
→ 保存当前工单 JSON
→ 尝试写入仪表
```

当前已经实现的回写规则：

| 当前仪表配置 | 本地配方参数 |
|---|---|
| ADEX DCR 工位一 | `DCR1` |
| ADEX DCR 工位二 | `DCR2` |
| Hioki Ls-Rs | `Ls`、`Rs` 与频率 |
| Hioki Z-θ，工位一 | `Z1`、`PHASE1` 与频率 |
| Hioki Z-θ，工位二 | `Z2`、`PHASE2` 与频率 |

### 7.3 StationView

StationView 表示物理工位当前是否由 PLC/HMI 开启。它不是工单参数编辑器。

最终是否执行某项测试的概念规则为：

```text
可执行 = 机型支持 && 工单要求 && PLC/HMI 工位启用
```

其中“工单要求”来自本地配方的 `Polarity.Enabled`、`Q.Enabled` 等字段。

## 8. 离线加载和应用时序

### 8.1 加载本地工单

```text
ParameterDictView 选择 {工单}.json
→ 点击导入
→ LocalWorkOrderRecipeStore.Load
→ LocalWorkOrderRecipeMapper.ToMesSetup
→ LocalWorkOrderRecipeLoadedMessage
→ HomeViewModel.ApplyLocalWorkOrderRecipeAsync
→ Machine.ApplyWorkOrderSetupAsync
→ 写入对应仪表 / PLC
→ 刷新 HomeView、SetView、图表、点检上下限
```

MES 在线时禁止该操作，防止本地值覆盖 MES 下发值。

### 8.2 离线修改一个工位

```text
MES 离线
→ 在 SetView 修改仪表参数
→ 点击应用
→ 配置校验
→ LocalWorkOrderRecipeMapper.UpdateFromInstrumentConfig
→ LocalWorkOrderRecipeStore.SaveAsync({工单}.json)
→ selectedConfigurableDevice.ApplyConfigAsync
→ 保存设备连接/仪表配置
→ 刷新 HomeView 限值与图表
```

### 8.3 仪表写入失败

```text
参数校验通过
→ 先保存 {工单}.json
→ 再写仪表
→ 写仪表失败
→ 显示“参数应用失败”
→ JSON 保留本次工程师输入
```

因此现场处理人员可以排查通讯、重新连接仪表后再次点击“应用”，不必重新输入全部参数。

如果 JSON 本身保存失败，则不继续写仪表，避免出现“仪表已变更但本地配方未留档”的不一致状态。

## 9. 单位与频率规则

配方存储的是**界面/工艺单位**，例如：

```text
Ls: 0.825 ~ 1.18 μH
Rs: 930 ~ 2500 mΩ
频率: 5 MHz
```

仪表下发时仍沿用现有单位转换链路，转换为仪表协议实际要求的 `H`、`Ω`、`Hz`。本地工单层不重复实现仪表协议转换，也不保存某品牌专用的指令格式。

## 10. 当前代码位置

| 职责 | 文件 |
|---|---|
| 本地 JSON 模型 | `KwyTemplate.App/Models/LocalWorkOrderRecipe.cs` |
| JSON 与统一工单模型转换 | `KwyTemplate.App/Services/LocalWorkOrderRecipeMapper.cs` |
| 本地文件读写 | `KwyTemplate.App/Services/LocalWorkOrderRecipeStore.cs` |
| 当前编辑配方会话 | `KwyTemplate.App/Services/LocalWorkOrderRecipeSession.cs` |
| 参数字典文件操作 | `KwyTemplate.App/ViewModels/ParameterDictViewModel.cs` |
| 本地配方加载与机型下发 | `KwyTemplate.App/ViewModels/HomeViewModel.cs` |
| 离线 SetView 回写 JSON | `KwyTemplate.App/ViewModels/SetViewModel.cs` |
| Cyntec 外部格式解析 | `KwyTemplate.MES.Cyntec/CyntecMesFileParser.cs` |

## 11. 尚未完成的下一阶段

当前 SetView 已可将仪表实际参数回写本地工单。是否测试不再维护独立的 `PolarityEnabled`、`QEnabled`、`SecondFrequencyEnabled` 开关：由各 HIOKI LCR 的实际参数对决定。选择 `OFF-OFF` 时，该仪表工位的两个测试参数均关闭；选择 `Z-θ`、`Ls-Rs` 或 `Ls-Q` 时，对应测量项自动写入配方。

`LocalWorkOrderFunctionOptions` 仅保留台纸料号、上盖料号与内部配方名称（Cyntec 的 `MatGroupNo` 映射到该字段）。`StdPartsCheck` 当前固定为 `4`，创建本地工单时写入默认值，不提供编辑控件。

## 12. 验证清单

1. MES 离线，新建 `0002595.json`，确认文件不是空 `{}`。
2. 导入本地 JSON，确认 Machine_2_A 只下发 DCR1/DCR2。
3. 导入同一 JSON，确认 Machine_4_HAHH 下发 DCR、Ls、Rs；极性工位选择 `Z-θ` 时下发 Z/θ，选择 `OFF-OFF` 时不下发极性测量项。
4. 离线修改 DCR 上下限并应用，确认 JSON 与 SetView 一致。
5. 离线修改 Ls/Rs、频率、单位并应用，确认 JSON 保存工艺单位，仪表按现有规则接收协议单位。
6. 断开仪表通讯后修改参数并应用，确认出现写入失败提示，但 JSON 内容已更新。
7. 重新连接仪表后导入同一 JSON，再应用，确认参数恢复。
8. MES 在线时点击导入本地 JSON，确认被阻止。
9. X 机种加载本地 JSON，确认 `equipmentType` 保留且不会重复弹出 Ls 下限输入框。
