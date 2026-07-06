# Kwy.UI.WPF.Charts

`Kwy.UI.WPF.Charts.Abstractions` 提供 WPF 图表模块的通用模型，不绑定具体图表引擎。

当前实现：

```text
Kwy.UI.WPF.Charts.Abstractions
  通用枚举、颜色板、环形缓存、基础通知对象、图表模型

Kwy.UI.WPF.Charts.OxyPlot
  基于 OxyPlot.Wpf 的图表实现

Kwy.UI.WPF.Charts.ScottPlot
  后续基于 ScottPlot.WPF 扩展
```

## 通用类型

| 类型 | 说明 |
| --- | --- |
| `ChartBindableBase` | 图表 ViewModel/封装对象使用的轻量属性通知基类。 |
| `CircularBuffer<T>` | 高吞吐实时图表用环形缓存。 |
| `PlotOrientation` | 横向/纵向图表方向。 |
| `PlotRefreshMode` | 波形图刷新模式：`Smooth`、`Step`、`Freeze`。 |
| `PlotPalettes` | 基于 key 的稳定颜色分配。 |
| `IChartPlot` | 通用图表生命周期入口：`IsActive`、`ClearData()`。 |
| `ChartOptions` | 图表标题、子标题等通用低频配置。 |
| `WaveformChartOptions` | 波形图低频配置，例如标题、时间轴名、值轴名、刷新模式。 |
| `HistogramChartOptions` | 直方图低频配置，例如方向、轴名、最小分箱宽度。 |
| `ScatterTrendChartOptions` | 散点趋势图低频配置，例如方向、窗口长度、轴名。 |
| `PieChartOptions` | 饼图低频配置，例如标题、内径。 |
| `TimelineChartOptions` | 时间线图低频配置，例如标题、时间轴名、工序轴名。 |

## OxyPlot 实现

`Kwy.UI.WPF.Charts.OxyPlot` 当前提供：

| 类型 | 场景 |
| --- | --- |
| `WaveformPlot` | 实时波形、IO 波形、模拟量曲线。 |
| `ScatterTrendPlot` | 测试结果趋势、Pass/Fail 散点趋势。 |
| `HistogramPlot` | 测试值分布、频次统计。 |
| `PiePlot` | Pass/Fail、类别占比。 |
| `TimelinePlot` | 工序时序、循环耗时、Gantt 风格流程追踪。 |

## WPF 使用示例

### ViewModel 对象方式

```csharp
public sealed class DemoViewModel
{
    public DemoViewModel()
    {
        Waveform = new WaveformPlot("DI Signals", timeWindow: 20000);
        Waveform.AddChannel("DI0", "DI0", isDigital: true);
        Waveform.AddChannel("AI0", "AI0");
    }

    public WaveformPlot Waveform { get; }

    public void AddSample(bool di0, double ai0)
    {
        Waveform.AddPoint("DI0", di0 ? 1 : 0);
        Waveform.AddPoint("AI0", ai0);
    }
}
```

XAML：

```xml
<oxy:PlotView
    Model="{Binding Waveform.Model}"
    Controller="{Binding Waveform.Controller}" />
```

需要引用命名空间：

```xml
xmlns:oxy="http://oxyplot.org/wpf"
```

### WPF 控件方式

`Kwy.UI.WPF.Charts.OxyPlot` 也提供 WPF 控件封装。标题、轴名、刷新模式等低频配置可以走 `DependencyProperty`，适合 XAML Binding、Style 和模板。

```xml
<charts:KwyWaveformPlotView
    Title="DI 波形"
    TimeAxisTitle="时间(ms)"
    ValueAxisTitle="状态"
    RefreshMode="Step"
    IsStacked="True" />
```

命名空间示例：

```xml
xmlns:kwycharts="http://schemas.kwy.com/ui/charts"
```

注意：实时数据点不建议通过依赖属性或绑定集合推送。高速数据仍应使用方法写入：

```csharp
waveformView.AddPoint("DI0", value);
```

这样可以避免 WPF Binding 和集合通知成为实时图表的性能瓶颈。

## 高频刷新优化

OxyPlot 实现层使用统一的渲染调度器。多个图表不会各自订阅 `CompositionTarget.Rendering`，而是由一个全局调度器集中分发，图表只有在 `IsActive = true` 时才参与刷新。

WPF 控件层会根据 `Loaded`、`Unloaded`、`IsVisible` 自动切换图表活动状态。导航缓存、隐藏 Tab、折叠区域里的图表不会继续后台刷新。

高频数据不要直接绑定集合，推荐批量或方法写入：

```csharp
waveform.AddPoint("AI0", value);
waveform.AddPoints("AI0", values);
histogram.AddValues(values);
```

当生产线程短时间写入大量数据时，图表会按帧分批消费，避免单帧一次性抽空队列造成 UI 卡顿：

```csharp
var waveform = new WaveformPlot(new WaveformChartOptions
{
    Title = "高速波形",
    TimeWindow = 30000,
    MaxPointsPerFrame = 20000
});

var histogram = new HistogramPlot(new HistogramChartOptions
{
    Title = "测试值分布",
    MaxValuesPerFrame = 20000
});
```

如果工控机配置较低，可以适当降低 `MaxPointsPerFrame` / `MaxValuesPerFrame`，让 UI 更稳定；如果需要更快追上积压数据，可以适当调高。

## 后续扩展 ScottPlot

ScottPlot 模块应复用 Abstractions 中的：

```text
ChartOptions
WaveformChartOptions
HistogramChartOptions
ScatterTrendChartOptions
PieChartOptions
TimelineChartOptions
PlotOrientation
PlotRefreshMode
PlotPalettes
CircularBuffer<T>
IChartPlot
```

但不要暴露 OxyPlot 类型。ScottPlot 实现可以提供同名语义的封装，例如 `ScottWaveformPlot`、`ScottHistogramPlot`，业务层根据需要选择 OxyPlot 或 ScottPlot 包。
