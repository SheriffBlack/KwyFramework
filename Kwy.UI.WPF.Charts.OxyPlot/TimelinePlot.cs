using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

public sealed class TimelineItem : IntervalBarItem
{
    public TimelineItem(double start, double end, string title = "", OxyColor color = default)
        : base(start, end, title)
    {
        ItemColor = color;
        if (color != default && color != OxyColors.Undefined)
        {
            Color = color;
        }
    }

    public string Label { get; set; } = string.Empty;

    public string Status { get; set; } = "Completed";

    public double Duration => Math.Abs(End - Start);

    public OxyColor ItemColor { get; set; }
}

public sealed class TimelineSeries : IntervalBarSeries
{
    public TimelineSeries(int capacity = 2000)
    {
        RingBuffer = new CircularBuffer<IntervalBarItem>(capacity);
        StrokeThickness = 0.5;
        StrokeColor = OxyColors.Black;
        LabelFormatString = "";
        LabelColor = OxyColors.Transparent;
    }

    public CircularBuffer<IntervalBarItem> RingBuffer { get; }

    protected override void UpdateData()
    {
        base.UpdateData();
        RingBuffer.CopyToList(Items);
    }
}

public sealed class TimelineChannel
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public OxyColor Color { get; set; } = OxyColors.Automatic;

    public int RowIndex { get; set; }

    public TimelineSeries Series { get; internal set; } = null!;

    public TimelineItem? ActiveItem { get; set; }
}

public sealed class TimelinePlot : ChartBindableBase, IChartPlot, IDisposable, IOxyRenderLoop
{
    private readonly PlotModel model;
    private readonly LinearAxis xAxis;
    private readonly CategoryAxis yAxis;
    private readonly List<TimelineChannel> channels = [];
    private readonly Dictionary<string, TimelineChannel> channelMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> registeredLegendLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Diagnostics.Stopwatch runtimeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private bool isActive;
    private bool isDirty;
    private string processAxisTitle = "Process";
    private double cycleStartTime;
    private double globalLastEnd;
    private double lastRenderTime;
    private string timeAxisTitle = "Time (ms)";
    private double viewWindow;

    public TimelinePlot(string title, double viewWindow = 30000)
        : this(new TimelineChartOptions { Title = title, ViewWindow = viewWindow })
    {
    }

    public TimelinePlot(TimelineChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        timeAxisTitle = options.TimeAxisTitle;
        processAxisTitle = options.ProcessAxisTitle;
        viewWindow = options.ViewWindow;
        this.viewWindow = options.ViewWindow;
        Controller = OxyChartHelpers.CreateTrackOnlyController();
        model = new PlotModel
        {
            Title = options.Title,
            Background = OxyColor.Parse("#1E1E1E"),
            TitleColor = OxyColor.Parse("#DCDCDC"),
            PlotAreaBorderThickness = new OxyThickness(0),
            SelectionColor = OxyColors.Transparent
        };
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightBottom,
            LegendPlacement = LegendPlacement.Outside,
            LegendOrientation = LegendOrientation.Vertical,
            LegendTextColor = OxyColor.Parse("#DCDCDC"),
            LegendFontSize = 12,
            LegendMaxWidth = 150
        });

        xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = TimeAxisTitle,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.Parse("#333333"),
            TextColor = OxyColor.Parse("#DCDCDC"),
            TitleColor = OxyColor.Parse("#DCDCDC"),
            AxislineStyle = LineStyle.Solid,
            AxislineColor = OxyColor.Parse("#3F3F46"),
            StringFormat = "0",
            Minimum = 0,
            Maximum = options.ViewWindow
        };
        yAxis = new CategoryAxis
        {
            Position = AxisPosition.Left,
            Title = ProcessAxisTitle,
            TextColor = OxyColor.Parse("#DCDCDC"),
            MajorGridlineStyle = LineStyle.None,
            AxislineColor = OxyColor.Parse("#3F3F46"),
            GapWidth = 0.6
        };

        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);
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

    public string TimeAxisTitle
    {
        get => timeAxisTitle;
        set
        {
            if (SetProperty(ref timeAxisTitle, value))
            {
                xAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public string ProcessAxisTitle
    {
        get => processAxisTitle;
        set
        {
            if (SetProperty(ref processAxisTitle, value))
            {
                yAxis.Title = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public double GlobalLastEnd => globalLastEnd;

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

    public void AddChannel(string key, string name, Color? color = null)
    {
        if (channelMap.ContainsKey(key))
        {
            return;
        }

        var finalColor = OxyChartHelpers.ToOxyColor(color ?? PlotPalettes.GetColor(key));
        var series = new TimelineSeries
        {
            Title = name,
            FillColor = finalColor,
            TrackerFormatString = "{Title}\nStart: {Start:0}ms\nDuration: {Duration:0}ms"
        };
        var channel = new TimelineChannel
        {
            Key = key,
            Name = name,
            Color = finalColor,
            RowIndex = channels.Count,
            Series = series
        };

        channels.Add(channel);
        channelMap[key] = channel;
        yAxis.Labels.Add(name);
        model.Series.Add(series);
        isDirty = true;
    }

    public void StartProcess(string key, string label = "")
    {
        if (!channelMap.TryGetValue(key, out var channel))
        {
            return;
        }

        double relativeTime = runtimeStopwatch.Elapsed.TotalMilliseconds - cycleStartTime;
        var item = new TimelineItem(relativeTime, relativeTime, label)
        {
            CategoryIndex = channel.RowIndex,
            Label = label,
            Status = "Running"
        };
        channel.ActiveItem = item;
        channel.Series.RingBuffer.Enqueue(item);
        isDirty = true;
    }

    public double AddStep(string key, double startTime, double duration, string label = "", Color? color = null)
    {
        if (!channelMap.TryGetValue(key, out var channel))
        {
            return startTime;
        }

        double end = startTime + duration;
        var finalColor = OxyChartHelpers.ToOxyColor(color ?? PlotPalettes.GetColor(label.Length > 0 ? label : key));
        var item = new TimelineItem(startTime, end, label, finalColor)
        {
            CategoryIndex = channel.RowIndex,
            Label = label,
            Status = "Completed"
        };

        if (!string.IsNullOrWhiteSpace(label) && registeredLegendLabels.Add(label))
        {
            lock (model.SyncRoot)
            {
                model.Series.Add(new ScatterSeries
                {
                    Title = label,
                    MarkerFill = finalColor,
                    MarkerType = MarkerType.Square,
                    MarkerSize = 6
                });
            }
        }

        channel.Series.RingBuffer.Enqueue(item);
        globalLastEnd = Math.Max(globalLastEnd, end);
        isDirty = true;
        return end;
    }

    public double AddSequentialStep(string key, double durationMs, string label = "", Color? color = null)
    {
        return AddStep(key, globalLastEnd, durationMs, label, color);
    }

    public void StopProcess(string key)
    {
        if (!channelMap.TryGetValue(key, out var channel) || channel.ActiveItem is null)
        {
            return;
        }

        channel.ActiveItem.End = runtimeStopwatch.Elapsed.TotalMilliseconds - cycleStartTime;
        channel.ActiveItem.Status = "Finished";
        channel.ActiveItem = null;
        isDirty = true;
    }

    public void ResetCycle()
    {
        cycleStartTime = runtimeStopwatch.Elapsed.TotalMilliseconds;
        globalLastEnd = 0;
        ClearData();
    }

    public void ClearData()
    {
        lock (model.SyncRoot)
        {
            foreach (var channel in channels)
            {
                channel.Series.RingBuffer.Clear();
                channel.ActiveItem = null;
            }

            isDirty = true;
        }
    }

    public void Dispose()
    {
        IsActive = false;
    }

    public void OnRenderFrame()
    {
        double now = runtimeStopwatch.Elapsed.TotalMilliseconds;
        if (now - lastRenderTime < 50)
        {
            return;
        }

        lastRenderTime = now;
        bool hasUpdate = false;
        foreach (var channel in channels)
        {
            if (channel.ActiveItem is not null)
            {
                channel.ActiveItem.End = now - cycleStartTime;
                hasUpdate = true;
            }
        }

        if (!hasUpdate && !isDirty)
        {
            return;
        }

        xAxis.Maximum = Math.Max(viewWindow, Math.Max(globalLastEnd, now - cycleStartTime));
        model.InvalidatePlot(true);
        isDirty = false;
    }
}
