using System.Collections.Concurrent;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

public sealed class PiePlot : ChartBindableBase, IChartPlot, IDisposable, IOxyRenderLoop
{
    private readonly PlotModel model;
    private readonly PieSeries pieSeries;
    private readonly ConcurrentQueue<PieSliceData> incomingUpdates = new();
    private readonly Dictionary<string, PieSlice> sliceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScatterSeries> legendSeriesMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> lastPercentages = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> keysOrder = [];
    private readonly System.Diagnostics.Stopwatch runtimeStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private bool isActive;
    private bool isDirty;
    private double lastRenderTime;

    public PiePlot(string title, double innerDiameter = 0.4)
        : this(new PieChartOptions { Title = title, InnerDiameter = innerDiameter })
    {
    }

    public PiePlot(PieChartOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Controller = OxyChartHelpers.CreateTrackOnlyController();

        model = new PlotModel
        {
            Title = options.Title,
            Background = OxyColor.Parse("#1E1E1E"),
            TitleColor = OxyColor.Parse("#DCDCDC"),
            PlotAreaBorderThickness = new OxyThickness(0),
            SelectionColor = OxyColors.Transparent
        };

        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = false });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, IsAxisVisible = false });
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightBottom,
            LegendPlacement = LegendPlacement.Outside,
            LegendOrientation = LegendOrientation.Vertical,
            LegendTextColor = OxyColor.Parse("#DCDCDC"),
            LegendFontSize = 13,
            LegendItemSpacing = 8,
            LegendMargin = 10,
            LegendMaxWidth = 250
        });

        pieSeries = new PieSeries
        {
            StrokeThickness = 0,
            InsideLabelFormat = null,
            OutsideLabelFormat = null,
            TickDistance = 0,
            InnerDiameter = options.InnerDiameter,
            StartAngle = 0,
            AngleSpan = 360,
            TrackerFormatString = "{1}\nValue: {2:0}\nPercent: {3:0.1}%"
        };

        model.Series.Add(pieSeries);
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

    public void AddOrUpdateSlice(string key, string label, double value, Color? color = null, bool isExploded = false)
    {
        incomingUpdates.Enqueue(new PieSliceData
        {
            Key = key,
            Label = label,
            Value = value,
            Color = color ?? PlotPalettes.GetColor(key),
            IsExploded = isExploded
        });
        isDirty = true;
    }

    public void ClearData()
    {
        lock (model.SyncRoot)
        {
            while (incomingUpdates.TryDequeue(out _))
            {
            }

            pieSeries.Slices.Clear();
            sliceMap.Clear();
            keysOrder.Clear();
            lastPercentages.Clear();

            foreach (var series in legendSeriesMap.Values)
            {
                model.Series.Remove(series);
            }

            legendSeriesMap.Clear();
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
        var newLegendSeries = new List<ScatterSeries>();

        while (incomingUpdates.TryDequeue(out var data))
        {
            hasUpdate = true;
            var fill = OxyChartHelpers.ToOxyColor(data.Color);
            var slice = new PieSlice(data.Label, data.Value)
            {
                Fill = fill,
                IsExploded = data.IsExploded
            };

            if (!sliceMap.ContainsKey(data.Key))
            {
                keysOrder.Add(data.Key);
                var legendSeries = new ScatterSeries
                {
                    Title = $"[  0.0%] {data.Label}",
                    MarkerType = MarkerType.Square,
                    MarkerFill = fill,
                    MarkerStroke = OxyColors.Transparent,
                    MarkerSize = 7
                };
                legendSeriesMap[data.Key] = legendSeries;
                newLegendSeries.Add(legendSeries);
            }

            sliceMap[data.Key] = slice;
        }

        if (!hasUpdate && !isDirty)
        {
            return;
        }

        lock (model.SyncRoot)
        {
            foreach (var series in newLegendSeries)
            {
                model.Series.Add(series);
            }

            pieSeries.Slices.Clear();
            double total = 0;
            foreach (string key in keysOrder)
            {
                if (sliceMap.TryGetValue(key, out var slice))
                {
                    pieSeries.Slices.Add(slice);
                    total += slice.Value;
                }
            }

            UpdateLegendPercentages(total);
            isDirty = false;
        }

        model.InvalidatePlot(true);
    }

    private void UpdateLegendPercentages(double total)
    {
        if (total <= 0)
        {
            return;
        }

        foreach (string key in keysOrder)
        {
            if (!sliceMap.TryGetValue(key, out var slice) || !legendSeriesMap.TryGetValue(key, out var legendSeries))
            {
                continue;
            }

            double percent = slice.Value / total * 100.0;
            bool shouldUpdate = !lastPercentages.TryGetValue(key, out double lastPercent)
                || Math.Abs(percent - lastPercent) >= 0.5
                || percent is 0.0 or 100.0;

            if (shouldUpdate)
            {
                lastPercentages[key] = percent;
                legendSeries.Title = $"[{percent,5:0.0}%] {slice.Label}";
            }
        }
    }
}
