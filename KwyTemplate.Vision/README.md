# KwyTemplate.Vision

`KwyTemplate.Vision` 是 `KwyTemplate` 中的视觉流程平台模块，定位是模板应用层模块，不是 Kwy 框架底层库。

它基于 `Kwy.MVVM.WPF`、`Kwy.UI.WPF`、`Kwy.UI.WPF.FlowDesigner` 和 `AvalonDock` 搭建，目标是提供类似海康、基恩士视觉平台的初级流程编辑、调试和运行体验。

## 模块职责

- 提供视觉流程编辑界面。
- 管理项目、流程图、节点、端口、连线和参数。
- 提供节点库、属性面板、运行日志、运行结果和流程保存/加载。
- 提供流程执行引擎，支持取消、停止、断点、单步、继续和运行结果汇总。
- 作为模板应用的视觉平台雏形，后续可接入 HALCON、OpenCV、相机、IO、PLC 等业务服务。

## 目录结构

```text
KwyTemplate.Vision
  VisionModule.cs                 MVVM 模块入口
  Models                          流程项目、流程图、节点、端口、连线模型
  ViewModels                      编辑器、节点库、属性面板、画布元素 VM
  Views                           WPF 视图
  Views/Templates                 节点、连线、探针、拖拽预览模板
  Themes                          DarkTheme / LightTheme
  NodeDescriptors                 节点 UI 元数据
  Executors                       节点执行器、执行上下文、端口值
  Registries                      节点描述符和执行器注册表
  Services                        保存加载、布局、执行、拖拽预览、颜色服务
```

## 当前能力

- 使用 AvalonDock 组织项目、节点库、流程编辑器、属性面板、运行记录和运行结果。
- 支持从节点库拖拽节点到画布。
- 支持节点连接、删除、自动布局、缩放适配。
- 支持连线数据探针，运行后在连线上显示传递值。
- 支持节点运行状态、节点耗时和失败提示。
- 支持运行记录面板，显示流程和节点的运行过程。
- 支持运行结果面板，显示每个节点的 OK/NG、类型、耗时和错误信息。
- 支持变量监控面板，按节点、方向、端口、数据类型和值查看运行时数据。
- 支持图像/ROI 面板，显示当前选中节点或最近运行节点的输入图、输出图、图像摘要和 Overlay 数量。
- 图像/ROI 面板支持滚轮缩放、右键/中键平移、双击适配窗口、鼠标像素坐标和值查看。
- 图像/ROI 面板支持绘制算法 Overlay，包括直线、圆、矩形、轮廓和文字。
- 图像/ROI 面板支持矩形 ROI 编辑：左键拖拽新建 ROI，拖动 ROI 主体移动，拖动 8 个控制点缩放，Delete 删除。
- 图像/ROI 面板会把当前 ROI 双向同步到选中节点参数：`RoiX`、`RoiY`、`RoiWidth`、`RoiHeight`。
- 支持节点搜索、收藏节点和只看收藏。
- 支持保存/加载 `.kproj` 流程项目。

## 节点命名规范

- `Math.*`：数学计算节点。
- `Logic.*`：流程控制和逻辑判断节点。
- `IO.*`：设备 IO 节点。
- `Vision.*`：传统视觉节点。
- `Vision.ObjectDetection.*`：深度学习目标检测节点。

## 已内置可运行节点

- `Math.NumberConstant`：输出一个固定数值。
- `Math.Add`：计算两个数值的和。
- `Logic.RangeJudgement`：根据上下限输出 OK/NG。
- `Logic.Delay`：延时等待。
- `Vision.ImageInput`：从文件读取图像；未配置路径时生成 1x1 占位图。
- `Vision.ImagePreprocess`：调用 `ImagePreprocess` 算法。
- `Vision.Threshold`：调用 `BlobInspection` 算法执行阈值分割。
- `Vision.Blob`：调用 `BlobInspection` 算法输出 Blob 集合。
- `Vision.Caliper`：调用 `CaliperGroupMeasurement` 算法输出卡尺结果。
- `Vision.LineFitting`：调用 `LineFitting` 算法。
- `Vision.CircleFitting`：调用 `CircleFitting` 算法。
- `Vision.TemplateMatching`：调用 `ShapeMatching` 算法。

其中 `Vision.ImagePreprocess`、`Vision.Threshold`、`Vision.Blob`、`Vision.Caliper`、`Vision.TemplateMatching` 支持读取 `RoiX / RoiY / RoiWidth / RoiHeight`。当 ROI 宽高大于 0 时：

- 预处理、阈值、Blob、模板匹配会把 ROI 作为搜索区域。
- 卡尺会优先把 ROI 作为测量区域。

## 已内置待接入节点

这些节点已经提供 Descriptor，后续按 `NodeType` 接入 HALCON、OpenCV、相机或设备服务：

- `IO.ReadDigitalInput`
- `IO.WriteDigitalOutput`
- `Vision.ObjectDetection.Yolo`

## 模块注册

在 Shell 或模板应用的模块目录中注册：

```csharp
moduleCatalog.AddModule<VisionModule>();
```

模块名定义在：

```csharp
KwyTemplate.Contracts.Modularity.ModuleNames.Vision
```

## 主题加载

推荐在 `App.xaml` 中按顺序加载：

```xml
<ResourceDictionary Source="pack://application:,,,/Kwy.UI.WPF;component/DefaultStyle.xaml" />
<ResourceDictionary Source="pack://application:,,,/Kwy.UI.WPF;component/Themes/LightTheme.xaml" />
<ResourceDictionary Source="pack://application:,,,/Kwy.UI.WPF.FlowDesigner;component/DefaultStyle.xaml" />
<ResourceDictionary Source="pack://application:,,,/Kwy.UI.WPF.FlowDesigner;component/Themes/LightTheme.xaml" />
<ResourceDictionary Source="pack://application:,,,/KwyTemplate.Vision;component/Themes/LightTheme.xaml" />
```

暗色主题将 `LightTheme.xaml` 换成 `DarkTheme.xaml` 即可。`KwyTemplate.Vision` 的主题中也包含 AvalonDock 的配色资源，因此停靠窗口会跟随 Light/Dark 切换。

## 执行引擎

`FlowExecutionService` 是流程运行内核，负责：

- 拓扑调度节点。
- 使用 `FlowExecutionContext` 传递本次运行上下文。
- 使用 `FlowValue` 区分“没有值”和“值为 null”。
- 返回 `FlowExecutionResult`，包含运行状态、错误节点、错误消息、执行数量、总耗时和每个节点的运行记录。
- 通过 `FlowRuntimeEvent` 实时输出流程开始、节点开始、节点完成、节点失败、调试暂停、流程完成等事件。
- 支持取消、停止、断点、单步和继续。

调试语义：

- `DebugCommand`：以调试模式运行流程。
- `StepCommand`：暂停时执行一步。
- `ContinueCommand`：从暂停处继续执行。
- `StopCommand`：取消当前执行。
- 连线上的 `HasBreakpoint` 是断点，流程执行到该连线下游节点前会暂停。

## 节点扩展规范

一个节点分两部分：

- `IFlowNodeDescriptor`：描述节点 UI 元数据、端口、默认参数和参数定义。
- `IFlowNodeExecutor`：执行节点运行时逻辑。

Descriptor 只负责“这个节点长什么样、有哪些端口和参数”；Executor 只负责“这个节点运行时做什么”。

### Descriptor 示例

```csharp
public sealed class MyNodeDescriptor : IFlowNodeDescriptor
{
    public string NodeType => "Vision.MyNode";
    public string DisplayName => "我的节点";
    public string Category => "图像处理";
    public string Description => "节点说明";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>("阈值", defaultValue: 128.0)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts =
        [
            new FlowPort { Name = "图像输入", Direction = PortDirection.Input, DataType = PortDataTypes.Image }
        ],
        OutputPorts =
        [
            new FlowPort { Name = "图像输出", Direction = PortDirection.Output, DataType = PortDataTypes.Image }
        ],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}
```

### Executor 示例

```csharp
public sealed class MyNodeExecutor : FlowNodeExecutorBase
{
    public override string NodeType => "Vision.MyNode";

    public override Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        double threshold = GetInputValue<double>(inputs, node, "阈值", 128.0);

        return Task.FromResult(FlowNodeExecutionResult.OkObjects(new Dictionary<string, object?>
        {
            ["图像输出"] = null
        }));
    }
}
```

## 参数面板

属性面板基于 `Kwy.ComponentModel.KwyParameterDefinition` 生成参数编辑器。

当前支持：

- `TextBox`
- `ComboBox`
- `ToggleButton`
- `DatePicker`
- `TextBlock`

节点 Descriptor 只需要声明参数定义：

```csharp
public IReadOnlyList<KwyParameterDefinition> Parameters =>
[
    KwyParameterDefinition.Create<double>("阈值", defaultValue: 128.0)
];
```

流程加载时，如果旧项目中只有参数字典、没有参数定义，属性面板会退化为普通文本编辑器。

## 与视觉算法层的关系

`KwyTemplate.Vision` 是平台界面和流程编排层，不直接等同于 HALCON/OpenCV 算法库。

推荐关系：

```text
KwyTemplate.Vision
  -> Kwy.Vision.Abstractions
  -> Kwy.Vision.Halcon / Kwy.Vision.OpenCV
```

也就是说：

- 平台节点负责参数、端口、流程调度和结果展示。
- 视觉算法实现放在 `Kwy.Vision.Halcon`、`Kwy.Vision.OpenCV` 等模块。
- Executor 通过抽象接口调用算法，避免 UI 模块直接绑定某个厂商后端。

## 后续建议

1. 将 ROI 编辑继续升级为完整 ROI 对象，支持旋转、命名、多 ROI 图层和 ROI 类型切换。
2. 为图像面板增加多图层开关、十字线、灰度剖面、局部放大镜和测量标尺。
3. 为节点收藏增加持久化，保存到用户配置。
4. 接入更多真实视觉节点：读码、OCR、轮廓检测、几何计算、标定和深度学习检测。
5. 增加流程执行、断点、保存加载和节点注册的单元测试。
