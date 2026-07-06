using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

public sealed class HistogramPlot : ChartBindableBase, IChartPlot, IDisposable, IOxyRenderLoop
{
    private readonly ConcurrentQueue<double> incomingData = new();
    private readonly PlotModel model;
    private readonly LinearAxis measurementAxis;
    private readonly LinearAxis frequencyAxis;
    private readonly PlotOrientation orientation;
    private readonly System.Diagnostics.Stopwatch runtimeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private HistogramChannel? channel;
    private string frequencyAxisTitle = "Frequency";
    private bool isActive;
    private bool isDirty;
    private double lastRenderTime;
    private double lastStep = -1;
    private readonly int maxValuesPerFrame;
    private double minBinWidth;
    private double? lowerLimit;
    private double? targetValue;
    private double? upperLimit;
    private string valueAxisTitle = "Value";

    public HistogramPlot(
        string title,
        string subtitle = "",
        PlotOrientation orientation = PlotOrientation.Vertical,
        double minBinWidth = 0.01)
        : this(new HistogramChartOptions
        {
            Title = title,
            Subtitle = subtitle,
            Orientation = orientation,
            MinBinWidth = minBinWidth
        })
    {
    }

    public HistogramPlot(HistogramChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        orientation = options.Orientation;
        this.orientation = options.Orientation;
        this.minBinWidth = options.MinBinWidth;
        maxValuesPerFrame = Math.Max(1, options.MaxValuesPerFrame);
        valueAxisTitle = options.ValueAxisTitle;
        frequencyAxisTitle = options.FrequencyAxisTitle;

        model = new PlotModel { Title = options.Title, Subtitle = options.Subtitle, PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 1) };
        measurementAxis = new LinearAxis
        {
            Position = options.Orientation == PlotOrientation.Horizontal ? AxisPosition.Left : AxisPosition.Bottom,
            Title = ValueAxisTitle,
            MajorGridlineStyle = LineStyle.Solid,
            StringFormat = "N2",
            MinorTickSize = 0,
            Key = "ValueAxis"
        };
        frequencyAxis = new LinearAxis
        {
            Position = options.Orientation == PlotOrientation.Horizontal ? AxisPosition.Bottom : AxisPosition.Left,
            Title = FrequencyAxisTitle,
            Minimum = 0,
            AbsoluteMinimum = 0,
            MinimumRange = 50,
            MinimumPadding = 0,
            MaximumPadding = 0.1,
            MinorTickSize = 0,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            LabelFormatter = value => Math.Abs(value) >= 1000 ? $"{value / 1000.0:0.#}k" : value.ToString("0")
        };

        model.Axes.Add(measurementAxis);
        model.Axes.Add(frequencyAxis);
        Controller = CreateController();
        OxyChartHelpers.RegisterWrapper(model, this);
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

    public string ValueAxisTitle
    {
        get => valueAxisTitle;
        set
        {
            if (SetProperty(ref valueAxisTitle, value))
            {
                measurementAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public string FrequencyAxisTitle
    {
        get => frequencyAxisTitle;
        set
        {
            if (SetProperty(ref frequencyAxisTitle, value))
            {
                frequencyAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public string HistogramSeriesTitle { get; set; } = "Histogram";

    public string UpperLimitLabel { get; set; } = "Upper";

    public string LowerLimitLabel { get; set; } = "Lower";

    public string TargetValueLabel { get; set; } = "Target";

    public double MinBinWidth
    {
        get => minBinWidth;
        set
        {
            if (SetProperty(ref minBinWidth, value))
            {
                ClearData();
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

        var oxyColor = OxyChartHelpers.ToOxyColor(color ?? PlotPalettes.GetColor(name));
        var series = new HistogramRectangleSeries
        {
            Title = name,
            Orientation = orientation,
            TrackerFormatString = "{0}\nRange: [{RangeMin:0.###}, {RangeMax:0.###})\nWidth: {Width:0.###}\nCount: {Count:0}"
        };
        var colorAxis = new LinearColorAxis
        {
            Position = AxisPosition.None,
            Palette = new OxyPalette(oxyColor),
            Key = "HistogramColorAxis"
        };
        series.ColorAxisKey = colorAxis.Key;

        channel = new HistogramChannel
        {
            Key = "Default",
            Name = name,
            Color = oxyColor,
            Series = series,
            ColorAxis = colorAxis
        };

        model.Axes.Add(colorAxis);
        model.Series.Add(series);
        isDirty = true;
    }

    public void AddValue(double value)
    {
        incomingData.Enqueue(value);
    }

    public void AddValues(IEnumerable<double> values)
    {
        foreach (double value in values)
        {
            incomingData.Enqueue(value);
        }
    }

    public void SetLimits(double? lower, double? upper, double? target)
    {
        lowerLimit = lower;
        upperLimit = upper;
        targetValue = target;
        UpdateAnnotations();

        if (lower.HasValue && upper.HasValue)
        {
            double range = upper.Value - lower.Value;
            if (range > 0)
            {
                double padding = range * 0.1;
                measurementAxis.Minimum = lower.Value - padding;
                measurementAxis.Maximum = upper.Value + padding;
            }
        }

        isDirty = true;
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
            channel.RawData.Clear();
            channel.BinnedData.Clear();
            channel.Series.Items.Clear();
            channel.Series.Counts.Clear();
            lastStep = -1;
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
        controller.BindMouseWheel(PlotCommands.ZoomWheel);
        controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
        controller.BindMouseEnter(PlotCommands.HoverSnapTrack);
        return controller;
    }

    public void OnRenderFrame()
    {
        double now = runtimeStopwatch.Elapsed.TotalMilliseconds;
        if (now - lastRenderTime < 50)
        {
            return;
        }

        lastRenderTime = now;
        if (isDirty || !incomingData.IsEmpty)
        {
            Refresh(false);
        }
    }

    private void Refresh(bool forceUpdateAxes)
    {
        if (channel is null)
        {
            return;
        }

        double currentStep = measurementAxis.ActualMajorStep;
        if (double.IsNaN(currentStep) || currentStep <= 0)
        {
            currentStep = 0.1;
        }

        double pixels = Math.Abs(measurementAxis.Transform(measurementAxis.ActualMaximum) - measurementAxis.Transform(measurementAxis.ActualMinimum));
        if (pixels <= 0)
        {
            pixels = 400;
        }

        double range = measurementAxis.ActualMaximum - measurementAxis.ActualMinimum;
        double targetBinStep = range / (pixels / 8.0);
        double ratio = currentStep / targetBinStep;
        double niceDivisor = ratio > 40 ? 50 : ratio > 15 ? 20 : ratio > 7 ? 10 : ratio > 3 ? 5 : ratio > 1.5 ? 2 : 1;
        double binStep = Math.Max(minBinWidth, currentStep / niceDivisor);
        bool stepChanged = Math.Abs(lastStep - binStep) > binStep * 0.001;
        bool hasNewData = !incomingData.IsEmpty;

        if (!forceUpdateAxes && !stepChanged && !hasNewData && isDirty)
        {
            isDirty = false;
            model.InvalidatePlot(false);
            return;
        }

        lock (model.SyncRoot)
        {
            isDirty = false;
            double quantStep = Math.Max(1e-5, minBinWidth / 10.0);
            int processed = 0;
            while (processed < maxValuesPerFrame && incomingData.TryDequeue(out double value))
            {
                processed++;
                long rawKey = (long)Math.Floor(value / quantStep);
                ref int rawCount = ref CollectionsMarshal.GetValueRefOrAddDefault(channel.RawData, rawKey, out _);
                rawCount++;

                if (!stepChanged && !forceUpdateAxes)
                {
                    long binnedKey = (long)Math.Floor(value / binStep + 1e-9);
                    ref int binnedCount = ref CollectionsMarshal.GetValueRefOrAddDefault(channel.BinnedData, binnedKey, out _);
                    binnedCount++;
                }
            }

            if (!incomingData.IsEmpty)
            {
                isDirty = true;
            }

            channel.Series.BinStep = binStep;
            if (stepChanged || forceUpdateAxes)
            {
                channel.BinnedData.Clear();
                foreach (var pair in channel.RawData)
                {
                    double approxValue = pair.Key * quantStep;
                    long binnedKey = (long)Math.Floor(approxValue / binStep + 1e-9);
                    ref int binnedCount = ref CollectionsMarshal.GetValueRefOrAddDefault(channel.BinnedData, binnedKey, out _);
                    binnedCount += pair.Value;
                }
            }

            lastStep = binStep;
            UpdateItems(channel, binStep);
        }

        model.InvalidatePlot(true);
    }

    private void UpdateItems(HistogramChannel histogramChannel, double binStep)
    {
        var items = histogramChannel.Series.Items;
        var counts = histogramChannel.Series.Counts;
        int requiredCount = histogramChannel.BinnedData.Count;
        if (items.Count > requiredCount)
        {
            items.RemoveRange(requiredCount, items.Count - requiredCount);
            counts.RemoveRange(requiredCount, counts.Count - requiredCount);
        }

        int index = 0;
        int maxCount = 0;
        double gap = binStep * 0.15;

        foreach (var pair in histogramChannel.BinnedData.OrderBy(pair => pair.Key))
        {
            double min = pair.Key * binStep;
            double max = min + binStep;
            int count = pair.Value;
            maxCount = Math.Max(maxCount, count);

            HistogramItem item = orientation == PlotOrientation.Horizontal
                ? new HistogramItem(0, count, min + gap, max - gap, min)
                : new HistogramItem(min + gap, max - gap, 0, count, min);
            item.RangeMin = min;
            item.RangeMax = max;
            item.Width = binStep;
            item.Count = count;

            if (index < items.Count)
            {
                items[index] = item;
                counts[index] = count;
            }
            else
            {
                items.Add(item);
                counts.Add(count);
            }

            index++;
        }

        frequencyAxis.Maximum = maxCount < 50 ? 50 : maxCount * 1.1;
    }

    private void UpdateAnnotations()
    {
        model.Annotations.Clear();
        AddLimitAnnotation(lowerLimit, LowerLimitLabel, OxyColors.Red, LineStyle.LongDash);
        AddLimitAnnotation(upperLimit, UpperLimitLabel, OxyColors.Red, LineStyle.LongDash);
        AddLimitAnnotation(targetValue, TargetValueLabel, OxyColors.Green, LineStyle.Solid);
    }

    private void AddLimitAnnotation(double? value, string label, OxyColor color, LineStyle lineStyle)
    {
        if (!value.HasValue)
        {
            return;
        }

        model.Annotations.Add(orientation == PlotOrientation.Horizontal
            ? new LineAnnotation { Type = LineAnnotationType.Horizontal, Y = value.Value, Color = color, LineStyle = lineStyle, Text = label }
            : new LineAnnotation { Type = LineAnnotationType.Vertical, X = value.Value, Color = color, LineStyle = lineStyle, Text = label });
    }
}

public sealed class HistogramChannel
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public OxyColor Color { get; set; } = OxyColors.Automatic;

    public Dictionary<long, int> RawData { get; } = [];

    public Dictionary<long, int> BinnedData { get; } = [];

    public HistogramRectangleSeries Series { get; internal set; } = new();

    public LinearColorAxis ColorAxis { get; internal set; } = new();
}

public sealed class HistogramRectangleSeries : RectangleSeries
{
    public PlotOrientation Orientation { get; set; }

    public double BinStep { get; set; }

    public List<double> Counts { get; } = [];
}

public sealed class HistogramItem : RectangleItem
{
    public HistogramItem(double x1, double x2, double y1, double y2, double value)
        : base(x1, x2, y1, y2, value)
    {
    }

    public double RangeMin { get; set; }

    public double RangeMax { get; set; }

    public double Width { get; set; }

    public double Count { get; set; }
}
