using System.Windows;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;

namespace Kwy.UI.WPF.Charts.OxyPlot.Controls;

public class KwyWaveformPlotView : global::OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(KwyWaveformPlotView), new PropertyMetadata(string.Empty, OnOptionChanged));

    public static readonly DependencyProperty TimeAxisTitleProperty =
        DependencyProperty.Register(nameof(TimeAxisTitle), typeof(string), typeof(KwyWaveformPlotView), new PropertyMetadata("Time (ms)", OnOptionChanged));

    public static readonly DependencyProperty ValueAxisTitleProperty =
        DependencyProperty.Register(nameof(ValueAxisTitle), typeof(string), typeof(KwyWaveformPlotView), new PropertyMetadata("Value", OnOptionChanged));

    public static readonly DependencyProperty RefreshModeProperty =
        DependencyProperty.Register(nameof(RefreshMode), typeof(PlotRefreshMode), typeof(KwyWaveformPlotView), new PropertyMetadata(PlotRefreshMode.Smooth, OnOptionChanged));

    public static readonly DependencyProperty IsStackedProperty =
        DependencyProperty.Register(nameof(IsStacked), typeof(bool), typeof(KwyWaveformPlotView), new PropertyMetadata(true, OnOptionChanged));

    public static readonly DependencyProperty TimeWindowProperty =
        DependencyProperty.Register(nameof(TimeWindow), typeof(double), typeof(KwyWaveformPlotView), new PropertyMetadata(20000.0));

    public KwyWaveformPlotView()
    {
        Chart = new WaveformPlot(CreateOptions());
        Model = Chart.Model;
        Controller = Chart.Controller;
        Chart.IsActive = false;
        Loaded += (_, _) => UpdateActiveState();
        IsVisibleChanged += (_, _) => UpdateActiveState();
        Unloaded += (_, _) => Chart.IsActive = false;
    }

    public WaveformPlot Chart { get; private set; }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string TimeAxisTitle
    {
        get => (string)GetValue(TimeAxisTitleProperty);
        set => SetValue(TimeAxisTitleProperty, value);
    }

    public string ValueAxisTitle
    {
        get => (string)GetValue(ValueAxisTitleProperty);
        set => SetValue(ValueAxisTitleProperty, value);
    }

    public PlotRefreshMode RefreshMode
    {
        get => (PlotRefreshMode)GetValue(RefreshModeProperty);
        set => SetValue(RefreshModeProperty, value);
    }

    public bool IsStacked
    {
        get => (bool)GetValue(IsStackedProperty);
        set => SetValue(IsStackedProperty, value);
    }

    public double TimeWindow
    {
        get => (double)GetValue(TimeWindowProperty);
        set => SetValue(TimeWindowProperty, value);
    }

    public void AddChannel(string key, string name, Color? color = null, bool isDigital = false, double? minY = null, double? maxY = null)
        => Chart.AddChannel(key, name, color, isDigital, minY, maxY);

    public void AddPoint(string key, double value)
        => Chart.AddPoint(key, value);

    public void ClearData()
        => Chart.ClearData();

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyWaveformPlotView view && view.Chart is not null)
        {
            view.ApplyOptions();
        }
    }

    private WaveformChartOptions CreateOptions()
    {
        return new WaveformChartOptions
        {
            Title = Title,
            TimeAxisTitle = TimeAxisTitle,
            ValueAxisTitle = ValueAxisTitle,
            RefreshMode = RefreshMode,
            IsStacked = IsStacked,
            TimeWindow = TimeWindow
        };
    }

    private void ApplyOptions()
    {
        Chart.Title = Title;
        Chart.TimeAxisTitle = TimeAxisTitle;
        Chart.DefaultYAxisTitle = ValueAxisTitle;
        Chart.RefreshMode = RefreshMode;
        Chart.IsStacked = IsStacked;
    }

    private void UpdateActiveState()
    {
        Chart.IsActive = IsLoaded && IsVisible;
    }
}
