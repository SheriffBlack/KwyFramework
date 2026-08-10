using System.Collections.Concurrent;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

public sealed class ScatterTrendSeries : ScatterSeries
{
    private readonly List<ScreenPoint> passBuffer = new(2000);
    private readonly List<ScreenPoint> failBuffer = new(2000);

    public ScatterTrendSeries(int capacity = 2000)
    {
        RingBuffer = new CircularBuffer<ScatterPoint>(capacity);
        MarkerType = MarkerType.Circle;
        MarkerStroke = OxyColors.Transparent;
    }

    public PlotOrientation Orientation { get; set; }

    public CircularBuffer<ScatterPoint> RingBuffer { get; }

    protected override void UpdateData()
    {
        base.UpdateData();
        RingBuffer.CopyToList(Points);
    }

    public override void Render(IRenderContext rc)
    {
        if (Points.Count == 0)
        {
            return;
        }

        passBuffer.Clear();
        failBuffer.Clear();
        foreach (var point in Points)
        {
            var screenPoint = XAxis.Transform(point.X, point.Y, YAxis);
            if (point.Value > 0.5)
            {
                failBuffer.Add(screenPoint);
            }
            else
            {
                passBuffer.Add(screenPoint);
            }
        }

        DrawMarkers(rc, passBuffer, MarkerFill);
        DrawMarkers(rc, failBuffer, OxyColors.Red);
    }

    private void DrawMarkers(IRenderContext rc, List<ScreenPoint> points, OxyColor fill)
    {
        if (points.Count == 0)
        {
            return;
        }

        rc.DrawMarkers(points, MarkerType, null, [MarkerSize], fill, MarkerStroke, MarkerStrokeThickness, EdgeRenderingMode);
    }
}

public sealed class ScatterTrendPlot : ChartBindableBase, IChartPlot, IDisposable, IOxyRenderLoop
{
    private const string SampleAxisKey = "SampleAxis";
    private const string ValueAxisKey = "ValueAxis";
    private readonly ConcurrentQueue<ScatterTrendSample> incomingData = new();
    private readonly PlotModel model;
    private readonly LinearAxis sampleAxis;
    private readonly LinearAxis valueAxis;
    private readonly PlotOrientation orientation;
    private readonly System.Diagnostics.Stopwatch runtimeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly List<LimitLineSeries> limitSeries = [];

    private ScatterTrendChannel? channel;
    private bool isActive;
    private bool isDirty;
    private double lastRenderTime;
    private readonly int maxValuesPerFrame;
    private string sampleAxisTitle = "Sample";
    private double sampleAxisMajorStep = double.NaN;
    private int viewWindow;
    private long globalSampleIndex;
    private double? lowerLimit;
    private double? targetValue;
    private double? upperLimit;
    private string valueAxisTitle = "Value";

    public ScatterTrendPlot(
        string title,
        string subtitle = "",
        PlotOrientation orientation = PlotOrientation.Horizontal,
        int viewWindow = 1000)
        : this(new ScatterTrendChartOptions
        {
            Title = title,
            Subtitle = subtitle,
            Orientation = orientation,
            ViewWindow = viewWindow
        })
    {
    }

    public ScatterTrendPlot(ScatterTrendChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        orientation = options.Orientation;
        this.orientation = options.Orientation;
        viewWindow = options.ViewWindow;
        this.viewWindow = options.ViewWindow;
        maxValuesPerFrame = Math.Max(1, options.MaxValuesPerFrame);
        sampleAxisTitle = options.SampleAxisTitle;
        sampleAxisMajorStep = options.SampleAxisMajorStep;
        valueAxisTitle = options.ValueAxisTitle;

        model = new PlotModel { Title = options.Title, Subtitle = options.Subtitle, PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 1) };
        sampleAxis = new LinearAxis
        {
            Position = options.Orientation == PlotOrientation.Horizontal ? AxisPosition.Bottom : AxisPosition.Left,
            Title = SampleAxisTitle,
            MinorTickSize = 0,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            Key = SampleAxisKey
        };
        ApplySampleAxisMajorStep();
        ApplySampleAxisWindow();
        valueAxis = new LinearAxis
        {
            Position = options.Orientation == PlotOrientation.Horizontal ? AxisPosition.Left : AxisPosition.Bottom,
            Title = ValueAxisTitle,
            MajorGridlineStyle = LineStyle.Solid,
            MinorTickSize = 0,
            LabelFormatter = value => OxyChartHelpers.FormatAdaptiveAxisLabel(value, valueAxis),
            Key = ValueAxisKey
        };

        model.Axes.Add(sampleAxis);
        model.Axes.Add(valueAxis);
        Controller = CreateController();
        IsActive = true;
    }

    public PlotModel Model => model;

    public IPlotController Controller { get; }

    public string Title
    {
        get => model.Title;
        set
        {
            if (model.Title == value)
            {
                return;
            }

            model.Title = value;
            OnPropertyChanged();
            model.InvalidatePlot(false);
        }
    }

    public string SampleAxisTitle
    {
        get => sampleAxisTitle;
        set
        {
            if (SetProperty(ref sampleAxisTitle, value))
            {
                sampleAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public double SampleAxisMajorStep
    {
        get => sampleAxisMajorStep;
        set
        {
            if (SetProperty(ref sampleAxisMajorStep, value))
            {
                ApplySampleAxisMajorStep();
                model.InvalidatePlot(false);
            }
        }
    }
    public string ValueAxisTitle
    {
        get => valueAxisTitle;
        set
        {
            if (SetProperty(ref valueAxisTitle, value))
            {
                valueAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public string UpperLimitLabel { get; set; } = "\u4E0A\u9650";

    public string LowerLimitLabel { get; set; } = "\u4E0B\u9650";

    public string TargetValueLabel { get; set; } = "\u6807\u51C6\u503C";

    public int ViewWindow
    {
        get => viewWindow;
        set
        {
            if (SetProperty(ref viewWindow, value))
            {
                ApplySampleAxisWindow();
                isDirty = true;
                model.InvalidatePlot(false);
            }
        }
    }

    public bool IsActive
    {
        get => isActive;
        set
        {
            if (!SetProperty(ref isActive, value))
            {
                return;
            }

            if (value)
            {
                OxyRenderScheduler.Register(this);
            }
            else
            {
                OxyRenderScheduler.Unregister(this);
            }
        }
    }

    public void AddChannel(string name, Color? color = null)
    {
        if (channel is not null)
        {
            model.Series.Remove(channel.Series);
            model.Axes.Remove(channel.ColorAxis);
        }

        var oxyColor = color.HasValue
            ? OxyChartHelpers.ToOxyColor(color.Value)
            : OxyChartHelpers.KwyTargetColor;
        var colorAxis = new CategoryColorAxis
        {
            Position = AxisPosition.None,
            Key = "ScatterColorAxis"
        };
        colorAxis.Palette.Colors.Add(oxyColor);
        colorAxis.Palette.Colors.Add(OxyColors.Red);

        var series = new ScatterTrendSeries
        {
            Title = name,
            Orientation = orientation,
            ColorAxisKey = colorAxis.Key,
            MarkerSize = 2.5,
            MarkerFill = oxyColor,
            TrackerFormatString = "{0}\nIndex: {SampleIndex}\nValue: {ActualValue:0.###}\nStatus: {Status}"
        };

        channel = new ScatterTrendChannel
        {
            Name = name,
            Color = oxyColor,
            Series = series,
            ColorAxis = colorAxis
        };

        model.Axes.Add(colorAxis);
        model.Series.Add(series);
        isDirty = true;
    }

    public void SetLimits(double? lower, double? upper, double? target)
    {
        lowerLimit = lower;
        upperLimit = upper;
        targetValue = target;
        UpdateLimitSeries();

        ApplyLimitAxisWindow();

        isDirty = true;
        model.InvalidatePlot(false);
    }

    public void AddValue(double value)
        => AddValue(value, null);

    public void AddValue(double value, bool? isPass)
    {
        incomingData.Enqueue(new ScatterTrendSample(value, isPass));
    }
    public void ClearData()
    {
        while (incomingData.TryDequeue(out _))
        {
        }

        if (channel is null)
        {
            return;
        }

        lock (model.SyncRoot)
        {
            channel.Series.Points.Clear();
            channel.Series.RingBuffer.Clear();
            globalSampleIndex = 0;
            ApplySampleAxisWindow();
            isDirty = true;
        }
    }

    public void Dispose()
    {
        IsActive = false;
    }

    private PlotController CreateController()
    {
        var controller = new PlotController();
        controller.BindMouseWheel(new DelegatePlotCommand<OxyMouseWheelEventArgs>((_, _, args) =>
        {
            double factor = args.Delta > 0 ? 1.1 : 1 / 1.1;
            double focalPoint = orientation == PlotOrientation.Horizontal
                ? valueAxis.InverseTransform(args.Position.Y)
                : valueAxis.InverseTransform(args.Position.X);
            valueAxis.ZoomAt(factor, focalPoint);
            model.InvalidatePlot(false);
            args.Handled = true;
        }));
        var resetCommand = new DelegatePlotCommand<OxyMouseDownEventArgs>((_, _, args) =>
        {
            ResetViewToLimits();
            args.Handled = true;
        });
        controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
        controller.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.None, 2, resetCommand);
        controller.BindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, 2, resetCommand);
        return controller;
    }

    public void ResetViewToLimits()
    {
        model.ResetAllAxes();
        ApplySampleAxisWindow();
        ApplyLimitAxisWindow();
        UpdateLimitSeries();
        isDirty = true;
        model.InvalidatePlot(false);
    }

    public void RefreshLimitLabels()
    {
        UpdateLimitSeries();
        isDirty = true;
        model.InvalidatePlot(false);
    }

    public void OnRenderFrame()
    {
        double now = runtimeStopwatch.Elapsed.TotalMilliseconds;
        if (now - lastRenderTime < 50)
        {
            return;
        }

        if (channel is null)
        {
            if (isDirty)
            {
                isDirty = false;
                ApplySampleAxisWindow();
                UpdateLimitSeries();
                model.InvalidatePlot(true);
            }

            return;
        }

        lastRenderTime = now;
        bool hasNewData = false;
        int processed = 0;
        while (processed < maxValuesPerFrame && incomingData.TryDequeue(out ScatterTrendSample sample))
        {
            processed++;
            hasNewData = true;
            globalSampleIndex++;
            double value = sample.Value;
            bool isFail = sample.IsPass.HasValue
                ? !sample.IsPass.Value
                : (upperLimit.HasValue && value > upperLimit.Value) || (lowerLimit.HasValue && value < lowerLimit.Value);
            double x = orientation == PlotOrientation.Horizontal ? globalSampleIndex : value;
            double y = orientation == PlotOrientation.Horizontal ? value : globalSampleIndex;
            channel.Series.RingBuffer.Enqueue(new ScatterTrendItem(x, y, double.NaN, isFail ? 1.0 : 0.0)
            {
                SampleIndex = globalSampleIndex,
                ActualValue = value,
                Status = isFail ? "FAIL" : "PASS"
            });
        }
        if (!incomingData.IsEmpty)
        {
            isDirty = true;
        }

        if (!hasNewData && !isDirty)
        {
            return;
        }

        lock (model.SyncRoot)
        {
            isDirty = false;
            ApplySampleAxisWindow();
            UpdateLimitSeries();

            double pruneThreshold = sampleAxis.Minimum - viewWindow * 0.1;
            if (orientation == PlotOrientation.Horizontal)
            {
                channel.Series.RingBuffer.DequeueWhile(point => point.X < pruneThreshold);
            }
            else
            {
                channel.Series.RingBuffer.DequeueWhile(point => point.Y < pruneThreshold);
            }
        }

        model.InvalidatePlot(true);
    }
    private void ApplySampleAxisWindow()
    {
        int effectiveViewWindow = Math.Max(1, viewWindow);
        double blank = effectiveViewWindow * 0.05;
        if (globalSampleIndex <= effectiveViewWindow)
        {
            sampleAxis.Minimum = 0;
            sampleAxis.Maximum = effectiveViewWindow;
        }
        else
        {
            sampleAxis.Minimum = globalSampleIndex - effectiveViewWindow;
            sampleAxis.Maximum = globalSampleIndex + blank;
        }
    }
    private void ApplySampleAxisMajorStep()
    {
        sampleAxis.MajorStep = double.IsFinite(sampleAxisMajorStep) && sampleAxisMajorStep > 0
            ? sampleAxisMajorStep
            : double.NaN;
    }
    private void UpdateLimitSeries()
    {
        foreach (LimitLineSeries series in limitSeries)
        {
            model.Series.Remove(series);
        }

        limitSeries.Clear();
        AddLimitSeries(lowerLimit, LowerLimitLabel, OxyChartHelpers.KwyLimitColor, LineStyle.LongDash);
        AddLimitSeries(upperLimit, UpperLimitLabel, OxyChartHelpers.KwyLimitColor, LineStyle.LongDash);
        AddLimitSeries(targetValue, TargetValueLabel, OxyChartHelpers.KwyTargetColor, LineStyle.Solid);
    }

    private void ApplyLimitAxisWindow()
    {
        List<double> values = [];
        AddFiniteLimitValue(values, lowerLimit);
        AddFiniteLimitValue(values, upperLimit);
        AddFiniteLimitValue(values, targetValue);
        if (values.Count == 0)
        {
            return;
        }

        double min = values.Min();
        double max = values.Max();
        double span = max - min;
        double padding = span > 0 ? span * 0.2 : Math.Max(Math.Abs(min) * 0.1, 1.0);
        valueAxis.Minimum = min - padding;
        valueAxis.Maximum = max + padding;
    }

    private static void AddFiniteLimitValue(ICollection<double> values, double? value)
    {
        if (value.HasValue && double.IsFinite(value.Value))
        {
            values.Add(value.Value);
        }
    }

    private void AddLimitSeries(double? value, string label, OxyColor color, LineStyle lineStyle)
    {
        if (!value.HasValue)
        {
            return;
        }

        double sampleMinimum = double.IsFinite(sampleAxis.Minimum) ? sampleAxis.Minimum : 0;
        double sampleMaximum = double.IsFinite(sampleAxis.Maximum) ? sampleAxis.Maximum : Math.Max(1, viewWindow);
        if (sampleMaximum <= sampleMinimum)
        {
            sampleMaximum = sampleMinimum + Math.Max(1, viewWindow);
        }

        var series = new LimitLineSeries
        {
            Title = label,
            LimitText = LimitLineSeries.FormatLabel(label, value.Value),
            Orientation = orientation,
            LimitTextColor = color,
            Color = color,
            LineStyle = lineStyle,
            StrokeThickness = 1.4,
            MarkerType = MarkerType.None,
            XAxisKey = orientation == PlotOrientation.Horizontal ? SampleAxisKey : ValueAxisKey,
            YAxisKey = orientation == PlotOrientation.Horizontal ? ValueAxisKey : SampleAxisKey
        };

        if (orientation == PlotOrientation.Horizontal)
        {
            series.Points.Add(new DataPoint(sampleMinimum, value.Value));
            series.Points.Add(new DataPoint(sampleMaximum, value.Value));
        }
        else
        {
            series.Points.Add(new DataPoint(value.Value, sampleMinimum));
            series.Points.Add(new DataPoint(value.Value, sampleMaximum));
        }

        limitSeries.Add(series);
        model.Series.Add(series);
    }
}

internal readonly record struct ScatterTrendSample(double Value, bool? IsPass);

public sealed class ScatterTrendChannel
{
    public string Name { get; set; } = string.Empty;

    public OxyColor Color { get; set; } = OxyColors.Automatic;

    public ScatterTrendSeries Series { get; internal set; } = new();

    public CategoryColorAxis ColorAxis { get; internal set; } = new();
}

public sealed class ScatterTrendItem : ScatterPoint
{
    public ScatterTrendItem(double x, double y, double size = double.NaN, double value = double.NaN, object? tag = null)
        : base(x, y, size, value, tag)
    {
    }

    public long SampleIndex { get; set; }

    public double ActualValue { get; set; }

    public string Status { get; set; } = string.Empty;
}



