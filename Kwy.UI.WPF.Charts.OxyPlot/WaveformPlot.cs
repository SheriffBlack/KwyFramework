using System.Collections.Concurrent;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

public sealed class FastLineSeries : LineSeries
{
    private readonly List<ScreenPoint> screenBuffer = new(4000);

    public FastLineSeries(int maxPoints)
    {
        RingBuffer = new CircularBuffer<DataPoint>(maxPoints);
    }

    public CircularBuffer<DataPoint> RingBuffer { get; }

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

        screenBuffer.Clear();
        var clippingRect = GetClippingRect();
        var first = XAxis.Transform(Points[0].X, Points[0].Y, YAxis);
        screenBuffer.Add(first);

        double currentX = Math.Round(first.X);
        int minIndex = 0;
        int maxIndex = 0;

        for (int i = 1; i < Points.Count - 1; i++)
        {
            var point = Points[i];
            double sx = XAxis.Transform(point.X);
            double sy = YAxis.Transform(point.Y);
            double roundedX = Math.Round(sx);

            if (roundedX == currentX)
            {
                if (point.Y < Points[minIndex].Y)
                {
                    minIndex = i;
                }
                else if (point.Y > Points[maxIndex].Y)
                {
                    maxIndex = i;
                }

                continue;
            }

            AddExtremaPoints(minIndex, maxIndex, i);
            screenBuffer.Add(new ScreenPoint(sx, sy));
            currentX = roundedX;
            minIndex = i;
            maxIndex = i;
        }

        screenBuffer.Add(XAxis.Transform(Points[^1].X, Points[^1].Y, YAxis));

        rc.PushClip(clippingRect);
        rc.DrawLine(screenBuffer, ActualColor, StrokeThickness, EdgeRenderingMode, LineStyle.GetDashArray(), LineJoin);
        rc.PopClip();
    }

    private void AddExtremaPoints(int minIndex, int maxIndex, int currentIndex)
    {
        if (minIndex < maxIndex)
        {
            screenBuffer.Add(XAxis.Transform(Points[minIndex].X, Points[minIndex].Y, YAxis));
            screenBuffer.Add(XAxis.Transform(Points[maxIndex].X, Points[maxIndex].Y, YAxis));
        }
        else if (maxIndex < minIndex)
        {
            screenBuffer.Add(XAxis.Transform(Points[maxIndex].X, Points[maxIndex].Y, YAxis));
            screenBuffer.Add(XAxis.Transform(Points[minIndex].X, Points[minIndex].Y, YAxis));
        }
        else if (minIndex != currentIndex - 1)
        {
            screenBuffer.Add(XAxis.Transform(Points[minIndex].X, Points[minIndex].Y, YAxis));
        }
    }
}

public sealed class WaveformChannel
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public OxyColor Color { get; set; } = OxyColors.Automatic;

    public bool IsDigital { get; set; }

    public double? MinY { get; set; }

    public double? MaxY { get; set; }

    public FastLineSeries Series { get; internal set; } = null!;

    public ConcurrentQueue<DataPoint> IncomingPoints { get; } = new();
}

public sealed class WaveformPlot : ChartBindableBase, IChartPlot, IDisposable, IOxyRenderLoop
{
    private readonly PlotModel model;
    private readonly LinearAxis xAxis;
    private readonly List<WaveformChannel> channels = [];
    private readonly Dictionary<string, WaveformChannel> channelMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Diagnostics.Stopwatch runtimeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly int maxPointsPerSeries;
    private readonly int maxPointsPerFrame;
    private string defaultYAxisTitle = "Value";
    private double lastRenderTime;
    private double lastStepRenderTime;
    private bool isActive;
    private bool isAutoFollow = true;
    private bool isDirty;
    private bool isInternalUpdating;
    private bool isStacked = true;
    private bool isYAxisZoomEnabled = true;
    private double scrollMax;
    private double scrollMin;
    private double scrollValue;
    private double scrollViewport;
    private double stepInterval = 500;
    private string timeAxisStringFormat = "0";
    private string timeAxisTitle = "Time (ms)";
    private double timeWindow;
    private double viewWindow;
    private PlotRefreshMode refreshMode = PlotRefreshMode.Smooth;

    public WaveformPlot(string title, double timeWindow = 20000, int maxPointsPerSeries = 100000)
        : this(new WaveformChartOptions
        {
            Title = title,
            TimeWindow = timeWindow,
            MaxPointsPerSeries = maxPointsPerSeries
        })
    {
    }

    public WaveformPlot(WaveformChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        timeAxisTitle = options.TimeAxisTitle;
        defaultYAxisTitle = options.ValueAxisTitle;
        timeAxisStringFormat = options.TimeAxisStringFormat;
        refreshMode = options.RefreshMode;
        isStacked = options.IsStacked;
        this.timeWindow = options.TimeWindow;
        viewWindow = options.TimeWindow;
        this.maxPointsPerSeries = options.MaxPointsPerSeries;
        maxPointsPerFrame = Math.Max(1, options.MaxPointsPerFrame);

        model = new PlotModel
        {
            Title = options.Title,
            Background = OxyColor.Parse("#1E1E1E"),
            TitleColor = OxyColor.Parse("#DCDCDC"),
            PlotAreaBorderColor = OxyColor.Parse("#3F3F46"),
            PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 1)
        };

        xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = TimeAxisTitle,
            StringFormat = TimeAxisStringFormat,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.Parse("#2D2D30"),
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.Parse("#252526"),
            TextColor = OxyColor.Parse("#999999"),
            TitleColor = OxyColor.Parse("#DCDCDC"),
            TicklineColor = OxyColor.Parse("#3F3F46"),
            MinimumPadding = 0,
            MaximumPadding = 0,
            AbsoluteMinimum = 0,
            IsPanEnabled = true,
            IsZoomEnabled = true
        };

        model.Axes.Add(xAxis);
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

    public string DefaultYAxisTitle
    {
        get => defaultYAxisTitle;
        set
        {
            if (SetProperty(ref defaultYAxisTitle, value))
            {
                UpdateStackingLayout();
                model.InvalidatePlot(false);
            }
        }
    }

    public string TimeAxisStringFormat
    {
        get => timeAxisStringFormat;
        set
        {
            if (SetProperty(ref timeAxisStringFormat, value))
            {
                xAxis.StringFormat = value;
                model.InvalidatePlot(false);
            }
        }
    }

    public PlotRefreshMode RefreshMode
    {
        get => refreshMode;
        set
        {
            if (SetProperty(ref refreshMode, value))
            {
                isDirty = true;
            }
        }
    }

    public double ViewWindow
    {
        get => viewWindow;
        set
        {
            double safeValue = Math.Min(value, timeWindow);
            if (SetProperty(ref viewWindow, safeValue) && isAutoFollow)
            {
                isDirty = true;
            }
        }
    }

    public double StepInterval
    {
        get => stepInterval;
        set => SetProperty(ref stepInterval, value);
    }

    public bool IsAutoFollow
    {
        get => isAutoFollow;
        set => SetProperty(ref isAutoFollow, value);
    }

    public bool IsYAxisZoomEnabled
    {
        get => isYAxisZoomEnabled;
        set
        {
            if (SetProperty(ref isYAxisZoomEnabled, value))
            {
                UpdateStackingLayout();
            }
        }
    }

    public bool IsStacked
    {
        get => isStacked;
        set
        {
            if (SetProperty(ref isStacked, value))
            {
                UpdateStackingLayout();
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

    public double ScrollMin
    {
        get => scrollMin;
        private set => SetProperty(ref scrollMin, value);
    }

    public double ScrollMax
    {
        get => scrollMax;
        private set => SetProperty(ref scrollMax, value);
    }

    public double ScrollViewport
    {
        get => scrollViewport;
        private set => SetProperty(ref scrollViewport, value);
    }

    public double ScrollValue
    {
        get => scrollValue;
        set
        {
            if (SetProperty(ref scrollValue, value) && !isInternalUpdating)
            {
                isAutoFollow = false;
                var span = xAxis.ActualMaximum - xAxis.ActualMinimum;
                xAxis.Minimum = value;
                xAxis.Maximum = value + span;
                isDirty = true;
            }
        }
    }

    public void AddChannel(string key, string name, Color? color = null, bool isDigital = false, double? minY = null, double? maxY = null)
    {
        if (channelMap.ContainsKey(key))
        {
            return;
        }

        var oxyColor = OxyChartHelpers.ToOxyColor(color ?? PlotPalettes.GetColor(key));
        var series = new FastLineSeries(maxPointsPerSeries)
        {
            Title = name,
            Color = oxyColor,
            StrokeThickness = isDigital ? 2 : 1.5,
            EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
        };

        var channel = new WaveformChannel
        {
            Key = key,
            Name = name,
            Color = oxyColor,
            IsDigital = isDigital,
            MinY = minY,
            MaxY = maxY,
            Series = series
        };

        channels.Add(channel);
        channelMap[key] = channel;
        model.Series.Add(series);
        UpdateStackingLayout();
    }

    public void AddPoint(string key, double value)
    {
        if (!channelMap.TryGetValue(key, out var channel))
        {
            return;
        }

        channel.IncomingPoints.Enqueue(new DataPoint(runtimeStopwatch.Elapsed.TotalMilliseconds, value));
        isDirty = true;
    }

    public void AddPoints(string key, IEnumerable<double> values)
    {
        if (!channelMap.TryGetValue(key, out var channel))
        {
            return;
        }

        double timestamp = runtimeStopwatch.Elapsed.TotalMilliseconds;
        foreach (double value in values)
        {
            channel.IncomingPoints.Enqueue(new DataPoint(timestamp, value));
        }

        isDirty = true;
    }

    public void ClearData()
    {
        foreach (var channel in channels)
        {
            while (channel.IncomingPoints.TryDequeue(out _))
            {
            }
        }

        lock (model.SyncRoot)
        {
            foreach (var channel in channels)
            {
                channel.Series.RingBuffer.Clear();
                channel.Series.Points.Clear();
            }

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
        controller.UnbindMouseDown(OxyMouseButton.Left);
        controller.UnbindMouseDown(OxyMouseButton.Right);

        var panCommand = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, ctr, args) =>
        {
            isAutoFollow = false;
            isDirty = true;
            PlotCommands.PanAt.Execute(view, ctr, args);
        });

        controller.BindMouseDown(OxyMouseButton.Left, panCommand);
        controller.BindMouseDown(OxyMouseButton.Right, panCommand);
        controller.BindMouseEnter(PlotCommands.HoverSnapTrack);
        controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.SnapTrack);
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
        bool hasNewData = FlushIncomingPoints();
        if (!hasNewData && !isAutoFollow && !isDirty)
        {
            return;
        }

        lock (model.SyncRoot)
        {
            UpdateXAxis(now, hasNewData);
            double pruneThreshold = Math.Min(now - timeWindow, xAxis.ActualMinimum);
            foreach (var channel in channels)
            {
                channel.Series.RingBuffer.DequeueWhile(point => point.X < pruneThreshold);
            }
        }

        UpdateScrollState(now);
        isDirty = false;
    }

    private bool FlushIncomingPoints()
    {
        bool hasNewData = false;
        int processed = 0;
        int perChannelLimit = Math.Max(1, maxPointsPerFrame / Math.Max(1, channels.Count));
        foreach (var channel in channels)
        {
            int channelProcessed = 0;
            while (processed < maxPointsPerFrame && channelProcessed < perChannelLimit && channel.IncomingPoints.TryDequeue(out var point))
            {
                processed++;
                channelProcessed++;
                hasNewData = true;
                if (channel.IsDigital && channel.Series.RingBuffer.Count > 0)
                {
                    var last = channel.Series.RingBuffer.GetLast();
                    if (last is { } lastPoint && !lastPoint.Y.Equals(point.Y))
                    {
                        channel.Series.RingBuffer.Enqueue(new DataPoint(point.X, lastPoint.Y));
                    }
                }

                channel.Series.RingBuffer.Enqueue(point);
            }

            if (!channel.IncomingPoints.IsEmpty)
            {
                isDirty = true;
            }
        }

        return hasNewData;
    }

    private void UpdateXAxis(double now, bool hasNewData)
    {
        if (RefreshMode == PlotRefreshMode.Freeze)
        {
            model.InvalidatePlot(hasNewData);
            return;
        }

        if (RefreshMode == PlotRefreshMode.Step && isAutoFollow)
        {
            if (xAxis.Maximum <= 0 || double.IsNaN(xAxis.Maximum))
            {
                xAxis.Maximum = Math.Max(viewWindow, now);
                xAxis.Minimum = xAxis.Maximum - viewWindow;
            }

            if (now >= xAxis.Maximum && now - lastStepRenderTime >= StepInterval)
            {
                double steps = Math.Max(1, Math.Ceiling((now - xAxis.Maximum) / StepInterval));
                xAxis.Maximum += steps * StepInterval;
                xAxis.Minimum = xAxis.Maximum - viewWindow;
                lastStepRenderTime = now;
                model.InvalidatePlot(true);
            }
            else if (hasNewData || isDirty)
            {
                model.InvalidatePlot(hasNewData);
            }

            return;
        }

        if (isAutoFollow)
        {
            double targetMax = Math.Max(viewWindow, now);
            isInternalUpdating = true;
            xAxis.Minimum = targetMax - viewWindow;
            xAxis.Maximum = targetMax;
            isInternalUpdating = false;
            model.InvalidatePlot(true);
            return;
        }

        model.InvalidatePlot(hasNewData);
    }

    private void UpdateScrollState(double now)
    {
        double span = xAxis.ActualMaximum - xAxis.ActualMinimum;
        isInternalUpdating = true;
        ScrollMin = now - timeWindow;
        ScrollMax = now;
        ScrollViewport = span;
        ScrollValue = xAxis.ActualMinimum;
        isInternalUpdating = false;
    }

    private void UpdateStackingLayout()
    {
        for (int i = model.Axes.Count - 1; i >= 0; i--)
        {
            if (model.Axes[i].Position != AxisPosition.Bottom)
            {
                model.Axes.RemoveAt(i);
            }
        }

        if (!IsStacked)
        {
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = DefaultYAxisTitle,
                MajorGridlineStyle = LineStyle.Solid,
                IsZoomEnabled = true,
                IsPanEnabled = true
            });

            foreach (var channel in channels)
            {
                channel.Series.YAxisKey = null;
            }

            return;
        }

        int count = channels.Count;
        if (count == 0)
        {
            return;
        }

        double slotHeight = 1.0 / (count + 0.15 * Math.Max(0, count - 1));
        double margin = slotHeight * 0.15;

        for (int i = 0; i < count; i++)
        {
            var channel = channels[count - 1 - i];
            double start = i * (slotHeight + margin);
            double end = start + slotHeight;
            var axis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = $"Y_{channel.Key}",
                Title = channel.Name,
                StartPosition = start,
                EndPosition = end,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.Parse("#2D2D30"),
                AxislineStyle = LineStyle.Solid,
                AxislineColor = channel.Color,
                TextColor = OxyColor.Parse("#999999"),
                TitleColor = channel.Color,
                TicklineColor = OxyColor.Parse("#3F3F46"),
                Minimum = channel.MinY ?? (channel.IsDigital ? -0.2 : double.NaN),
                Maximum = channel.MaxY ?? (channel.IsDigital ? 1.2 : double.NaN),
                MajorStep = channel.IsDigital ? 1 : double.NaN,
                MinorStep = channel.IsDigital ? 1 : double.NaN,
                IsZoomEnabled = IsYAxisZoomEnabled,
                IsPanEnabled = IsYAxisZoomEnabled
            };

            channel.Series.YAxisKey = axis.Key;
            model.Axes.Add(axis);
        }
    }
}
