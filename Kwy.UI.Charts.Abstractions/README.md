# Kwy UI 图表分层

图表模块把“图表语义”、“WPF 控件”和“渲染引擎”分开：

```text
Kwy.UI
└─ Primitives/Rgba32                 跨 UI 框架的颜色值

Kwy.UI.Charts.Abstractions              net8.0
├─ Definitions                         低频图表定义
├─ Updates                             高频数据更新
└─ Rendering                           后端与会话契约

Kwy.UI.WPF.Charts                       net8.0-windows
└─ Controls                            WPF 门面控件
   ├─ KwyChartView                     通用图表宿主
   ├─ KwyWaveformPlotView              波形图门面
   ├─ KwyScatterTrendPlotView           散点趋势图门面
   ├─ KwyHistogramPlotView              直方图门面
   ├─ KwyPiePlotView                    饼图门面
   └─ KwyTimelinePlotView               时间线图门面

Kwy.UI.WPF.Charts.OxyPlot
└─ Rendering/OxyChartBackend           OxyPlot 适配器

Kwy.UI.WPF.Charts.ScottPlot
└─ Rendering/ScottPlotChartBackend     未来 ScottPlot 适配器
```

## 依赖规则

- 抽象层不引用 WPF、OxyPlot 或 ScottPlot。
- WPF 门面只认识抽象层，不认识任何图表引擎。
- 实现包同时引用抽象层和 WPF 门面，业务项目只需引用一个实现包。
- 业务 XAML 使用 `KwyChartView`、`KwyScatterTrendPlotView` 等门面，不直接使用 `OxyPlot.Wpf.PlotView`。
- 业务数据通过 `IChartUpdateSink.Apply(ChartUpdate)` 推送，不依赖后端控件类型。

## WPF 使用

应用层只需引用选定的实现项目，例如 `Kwy.UI.WPF.Charts.OxyPlot`。其余两层由项目引用传递带入。

```xml
<UserControl
    xmlns:charts="http://schemas.kwy.com/ui/charts">
    <UserControl.Resources>
        <charts:OxyChartBackend x:Key="ChartBackend" />
    </UserControl.Resources>

    <charts:KwyScatterTrendPlotView
        Backend="{StaticResource ChartBackend}"
        charts:ChartValuesBinding.Point="{Binding LatestPoint}"
        charts:ChartValuesBinding.Points="{Binding Points}"
        charts:ChartValuesBinding.Limits="{Binding Limits}"
        charts:ChartValuesBinding.ResetKey="{Binding ParameterId}"
        Title="{Binding DisplayName}"
        SampleAxisTitle="Sample"
        ValueAxisTitle="Value" />
</UserControl>
```

通用场景可直接使用 `KwyChartView` 并绑定 `ChartDefinition`；常用 WPF 场景使用强类型门面控件，以便在 XAML 中绑定标题和坐标轴。

`ChartValuesBinding` 面向常规业务，直接接收跨平台的 `ChartValuePoint`、`ChartLimitRange` 和重置键，并转换为图表更新。`ChartUpdatesBinding` 面向高级场景，负责把已经构造好的 `ChartUpdate` 或其集合直接转发给门面控件。只有无法用这两种公共绑定表达的特殊业务，才需要在应用项目中编写专用行为。

## 更换后端

新增 ScottPlot 实现时，实现 `IChartBackend<FrameworkElement>` 和 `IChartSession<FrameworkElement>`。应用层只需将资源中的 `OxyChartBackend` 替换为 `ScottPlotChartBackend`，门面控件、绑定和数据推送行为保持不变。
