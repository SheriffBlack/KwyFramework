# KwyDiagramCanvas 使用教程

`KwyDiagramCanvas` 是 `Kwy.UI.WPF` 提供的轻量二维图控件，用于展示设备拓扑、工艺示意图、网络关系或状态总览。它不依赖业务模型：`Kwy.UI` 只定义图节点、连线和布局契约，WPF 项目负责绘制、主题和交互。

> 本文随 `Kwy.UI` 包附带，以便使用者同时了解跨平台数据契约；实际控件位于 `Kwy.UI.WPF`，使用 WPF 画布时需要引用该包。

## 1. 适用边界

适合：

- 固定或低频变化的二维设备拓扑；
- 节点状态、选中状态和简单连接关系的可视化；
- 可通过逻辑坐标表达的工艺示意图。

不适合：

- 任意节点编辑、端口连线、框选、多选、撤销重做等流程设计器场景；这类场景使用 `Kwy.UI.WPF.FlowDesigner`；
- 海量实时点位的曲线绘制；这类场景使用图表控件；
- 需要为每个节点嵌入复杂 WPF 控件、菜单或动画的自由布局界面。

`KwyDiagramCanvas` 的目标是“展示”，而不是通用流程编辑器。

## 2. 引用与 XAML 命名空间

项目同时引用 `Kwy.UI` 与 `Kwy.UI.WPF` 后，在 XAML 中声明：

```xml
xmlns:kwyui="http://schemas.kwy.com/ui"
```

最小使用示例：

```xml
<kwyui:KwyDiagramCanvas
    Background="{DynamicResource TransparentBrush}"
    Connections="{Binding Connections}"
    NodeClickCommand="{Binding SelectNodeCommand}"
    Nodes="{Binding Nodes}"
    SelectedNode="{Binding SelectedNode, Mode=TwoWay}"
    Zoom="1" />
```

画布会先绘制连线，再绘制节点。因此连线端点会自然隐藏在节点下方，不需要业务代码额外裁剪。

## 3. 数据模型

### 3.1 DiagramPoint

```csharp
public readonly record struct DiagramPoint(double X, double Y);
```

它是逻辑坐标，不绑定 WPF 像素、屏幕 DPI 或具体 UI 框架。`KwyDiagramCanvas` 通过 `Zoom` 和 `PanOffset` 将逻辑坐标映射到当前画布。

### 3.2 DiagramNode

```csharp
public sealed record DiagramNode(
    string Id,
    double X,
    double Y,
    double Width,
    double Height,
    string? Label = null,
    DiagramNodeState State = DiagramNodeState.Normal,
    DiagramNodeShape Shape = DiagramNodeShape.RoundedRectangle,
    DiagramNodeOrientation Orientation = DiagramNodeOrientation.Top,
    int PolygonSides = 0,
    double RotationAngle = 0d,
    bool UseStateFill = false,
    DiagramNodeFillMode FillMode = DiagramNodeFillMode.Default);
```

`X`、`Y` 是节点左上角坐标；`Id` 在一个画布内必须唯一。无效坐标、非正宽高或空 `Id` 会被控件忽略，不会造成渲染异常。

`DiagramNode` 是不可变记录。状态或位置变化时，用 `with` 创建新实例并替换集合中的旧项：

```csharp
int index = Nodes.IndexOf(Nodes.First(node => node.Id == "pm-4"));
Nodes[index] = Nodes[index] with { State = DiagramNodeState.Warning };
```

### 3.3 DiagramConnection

```csharp
public sealed record DiagramConnection(
    string SourceId,
    string TargetId,
    string? Label = null,
    DiagramConnectionState State = DiagramConnectionState.Normal,
    double Thickness = 1d,
    double? TargetThickness = null,
    DiagramConnectionShape Shape = DiagramConnectionShape.Line);
```

连接通过节点 `Id` 建立。引用不存在节点的连接会被安全忽略。`Thickness` 用于表达主干、支路等视觉层级；无效或非正值按 `1` 处理。当前控件只绘制连接线，`Label` 作为上层保留的语义字段，不会自动绘制在线上。

默认 `Shape` 是等宽的 `Line`。当设备外壳需要与腔体形成机械连接臂时，使用 `Tapered`：`Thickness` 表示源端宽度，`TargetThickness` 表示目标端宽度。

```csharp
new DiagramConnection(
    "pm-1",
    "chamber-shell",
    State: DiagramConnectionState.Active,
    Thickness: 54d,
    TargetThickness: 24d,
    Shape: DiagramConnectionShape.Tapered);
```

画布会将该连接绘制为四边形，再由两端节点覆盖端点，使连接臂自然贴合 PM 外壳和腔体轮廓。

## 4. 节点外形

`DiagramNodeShape` 包含以下外形：

| 枚举值 | 适合表达的对象 |
| --- | --- |
| `Rectangle` | 柜体、气体供应、总线或普通模块 |
| `RoundedRectangle` | 电源、控制模块、逻辑单元 |
| `Ellipse` | 泵、阀、传感器、状态核心 |
| `SemiEllipse` | Load Lock、Cooling 等圆顶模块 |
| `Trapezoid` | Showerhead、漏斗、方向性工艺部件 |
| `RegularPolygon` | 腔体、平台或多边形设备主体 |

示例：

```csharp
Nodes =
[
    new("gas", 520d, 40d, 120d, 50d, "Gas Supply", Shape: DiagramNodeShape.Rectangle),
    new("rf", 60d, 40d, 120d, 50d, "RF Match", Shape: DiagramNodeShape.RoundedRectangle),
    new("pump", 300d, 80d, 62d, 62d, "PM1", DiagramNodeState.Active, DiagramNodeShape.Ellipse, UseStateFill: true),
    new("load-lock", 260d, 330d, 80d, 50d, "Load Lock", Shape: DiagramNodeShape.SemiEllipse),
    new("showerhead", 320d, 160d, 88d, 40d, "Showerhead", Shape: DiagramNodeShape.Trapezoid),
    new("chamber", 268d, 126d, 184d, 184d, "Process Chamber", DiagramNodeState.Active, DiagramNodeShape.RegularPolygon, PolygonSides: 8)
];
```

### 4.1 半圆加矩形底座

`SemiEllipse` 是“半圆 + 与其直径同宽的矩形底座”。`Orientation` 决定圆顶方向：

```csharp
new DiagramNode(
    "load-lock",
    260d,
    330d,
    80d,
    50d,
    "Load Lock",
    Shape: DiagramNodeShape.SemiEllipse,
    Orientation: DiagramNodeOrientation.Top);
```

通过 `SemiEllipseBaseRatio` 调整底座的最小高度比例：

```xml
<kwyui:KwyDiagramCanvas SemiEllipseBaseRatio="0.18" />
```

为保持圆顶接近真正半圆，控件在节点高度允许时会优先保留半圆所需高度；因此实际底座可能略大于该最小比例。

### 4.2 多边形和旋转

`RegularPolygon` 的边数由 `PolygonSides` 决定，最小为 3。`RotationAngle` 以节点中心为原点旋转所有外形：

```csharp
new DiagramNode(
    "octagon",
    240d,
    120d,
    160d,
    160d,
    "Chamber",
    Shape: DiagramNodeShape.RegularPolygon,
    PolygonSides: 8,
    RotationAngle: 22.5d);
```

## 5. 状态、主题和状态填充

节点与连线均支持：`Normal`、`Active`、`Disabled`、`Warning`、`Error`。

默认情况下，状态反映在节点边框和连接线。`DiagramNodeFillMode` 用于区分结构件和状态核心：

| 填充模式 | 适用场景 |
| --- | --- |
| `Default` | 普通白底节点 |
| `State` | PM、阀、端口等需要高饱和状态色的核心节点 |
| `SoftState` | 腔体外壳、传输腔、面板等需要保留结构层级的背景节点 |

`UseStateFill: true` 保持为 `State` 填充的简洁写法。对于 PM 这类需要突出状态的圆形核心，设置 `UseStateFill="true"` 或 `UseStateFill: true`，控件会将状态色用于填充，并自动使用白色文本：

```csharp
new DiagramNode(
    "pm-4",
    450d,
    260d,
    62d,
    62d,
    "PM4",
    DiagramNodeState.Active,
    DiagramNodeShape.Ellipse,
    UseStateFill: true);
```

腔体或传输腔等结构件应使用浅色填充，避免压过状态核心：

```csharp
new DiagramNode(
    "transfer-chamber",
    120d,
    380d,
    480d,
    92d,
    Shape: DiagramNodeShape.Rectangle,
    State: DiagramNodeState.Active,
    FillMode: DiagramNodeFillMode.SoftState);
```

控件使用 Kwy 主题资源，而不固定颜色：

- `PrimaryBrush`：活动状态；
- `WarningBrush`、`ErrorBrush`：告警和异常状态；
- `ControlBorderBrush`：普通边框与普通连线；
- `ControlDisabledForegroundBrush`：禁用状态；
- `ControlBackgroundBrush`、`ForegroundBrush`：普通节点填充与文本。

因此切换 Light/Dark 主题时，无需替换节点模型或业务代码。

## 6. 布局：不要散落绝对坐标

任意拓扑最终都需要位置，但不应在 ViewModel 中散落难以维护的数值，例如：

```csharp
// 不建议：位置含义不清晰，结构调整困难。
new DiagramNode("showerhead", 316d, 160d, 88d, 40d, "Showerhead");
```

推荐以设备中心、边界、尺寸和偏移量推导位置：

```csharp
const double chamberSize = 184d;
const double showerheadWidth = 88d;
const double showerheadHeight = 40d;

var chamberCenter = new DiagramPoint(360d, 218d);
double chamberTop = chamberCenter.Y - (chamberSize / 2d);

DiagramNode showerhead = CreateCenteredNode(
    "showerhead",
    new DiagramPoint(chamberCenter.X, chamberTop + 54d),
    showerheadWidth,
    showerheadHeight,
    "Showerhead",
    DiagramNodeShape.Trapezoid);
```

外围节点优先使用 `RadialDiagramLayout`：

```csharp
IReadOnlyList<DiagramPoint> pumpCenters = RadialDiagramLayout.Arrange(
    center: new DiagramPoint(360d, 218d),
    radius: 142d,
    count: 6,
    startAngle: -150d);
```

该算法位于 `Kwy.UI`，不依赖 WPF；业务项目可据此组合自己的设备结构。设备专有的布局规则应留在应用层，不应放入 `Kwy.UI` 或 `Kwy.UI.WPF`。

## 7. 选中与点击

`SelectedNode` 是双向绑定属性。用户点击有效节点后，控件会更新选中节点，并把该 `DiagramNode` 作为参数调用 `NodeClickCommand`：

```xml
<kwyui:KwyDiagramCanvas
    NodeClickCommand="{Binding SelectNodeCommand}"
    SelectedNode="{Binding SelectedNode, Mode=TwoWay}" />
```

```csharp
public DelegateCommand<DiagramNode> SelectNodeCommand
    => selectNodeCommand ??= new DelegateCommand<DiagramNode>(node => SelectedNode = node);
```

控件只负责命中测试、选中与命令转发。打开详情、修改设备状态、权限判断等业务行为由应用层命令处理。

## 8. 缩放、平移与节点样式

```xml
<kwyui:KwyDiagramCanvas
    NodeCornerRadius="8"
    PanOffset="{Binding DiagramOffset}"
    SemiEllipseBaseRatio="0.18"
    Zoom="1.25" />
```

| 属性 | 说明 |
| --- | --- |
| `Zoom` | 逻辑坐标缩放比例，必须大于 0 |
| `PanOffset` | 逻辑坐标平移量 |
| `NodeCornerRadius` | `RoundedRectangle` 的圆角半径 |
| `SemiEllipseBaseRatio` | `SemiEllipse` 矩形底座的最小高度比例，范围 `[0, 0.5)` |

当前版本提供基础缩放和平移变换。若需要鼠标滚轮缩放、拖拽平移、缩放边界或适配内容大小，应由宿主应用根据自身交互规范封装，而不应让设备业务模型承担 UI 线程或输入协调职责。

## 9. 集合更新与生命周期

`Nodes`、`Connections` 推荐使用 `ObservableCollection<T>`。控件会订阅实现 `INotifyCollectionChanged` 的集合，在集合增删改时重绘；离开视觉树或更换集合时会自动解除订阅。

```csharp
public ObservableCollection<DiagramNode> Nodes { get; } = [];
public ObservableCollection<DiagramConnection> Connections { get; } = [];
```

由于节点和连线是不可变记录，请使用“替换集合项”而不是修改不存在的可变属性。若一次更新大量节点，建议先在本地构建新集合，再一次性替换 `Nodes` 引用或批量更新，避免每一个小变更都触发重绘。

## 10. 性能建议

- 保持节点、连线集合为业务所需的最小规模；
- 高频采样数据不要直接映射为节点位置逐点刷新；应采样、节流或交给图表控件；
- 状态高频变化时，仅替换受影响节点；
- 对静态设备结构，复用节点定义，只更新状态；
- 不要在 `DiagramNode` 中保存 Dispatcher、控件引用或业务服务。

## 11. 分层建议

```text
Kwy.UI
  DiagramPoint / DiagramNode / DiagramConnection / RadialDiagramLayout
          ↓
Kwy.UI.WPF
  KwyDiagramCanvas（绘制、主题、命中、选中）
          ↓
业务应用
  设备名称、工艺结构、状态来源、布局参数、点击命令
```

这样 `Kwy.UI` 保持纯数据契约，`Kwy.UI.WPF` 保持 WPF 表现层，PECVD、真空、搬运或网络监控等不同业务可复用同一画布而不向 UI 库引入业务依赖。
