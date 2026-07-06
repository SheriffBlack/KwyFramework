using System.Windows;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;

namespace Kwy.UI.WPF.Charts.OxyPlot.Controls;

public class KwyHistogramPlotView : global::OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(KwyHistogramPlotView), new PropertyMetadata(string.Empty, OnOptionChanged));

    public static readonly DependencyProperty ValueAxisTitleProperty =
        DependencyProperty.Register(nameof(ValueAxisTitle), typeof(string), typeof(KwyHistogramPlotView), new PropertyMetadata("Value", OnOptionChanged));

    public static readonly DependencyProperty FrequencyAxisTitleProperty =
        DependencyProperty.Register(nameof(FrequencyAxisTitle), typeof(string), typeof(KwyHistogramPlotView), new PropertyMetadata("Frequency", OnOptionChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(PlotOrientation), typeof(KwyHistogramPlotView), new PropertyMetadata(PlotOrientation.Vertical, OnRecreateRequired));

    public KwyHistogramPlotView()
    {
        CreateChart();
        Loaded += (_, _) => UpdateActiveState();
        IsVisibleChanged += (_, _) => UpdateActiveState();
        Unloaded += (_, _) => Chart.IsActive = false;
    }

    public HistogramPlot Chart { get; private set; } = null!;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ValueAxisTitle
    {
        get => (string)GetValue(ValueAxisTitleProperty);
        set => SetValue(ValueAxisTitleProperty, value);
    }

    public string FrequencyAxisTitle
    {
        get => (string)GetValue(FrequencyAxisTitleProperty);
        set => SetValue(FrequencyAxisTitleProperty, value);
    }

    public PlotOrientation Orientation
    {
        get => (PlotOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public void AddChannel(string name, Color? color = null) => Chart.AddChannel(name, color);

    public void AddValue(double value) => Chart.AddValue(value);

    public void AddValues(IEnumerable<double> values) => Chart.AddValues(values);

    public void SetLimits(double? lower, double? upper, double? target) => Chart.SetLimits(lower, upper, target);

    public void ClearData() => Chart.ClearData();

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyHistogramPlotView view && view.Chart is not null)
        {
            view.Chart.Title = view.Title;
            view.Chart.ValueAxisTitle = view.ValueAxisTitle;
            view.Chart.FrequencyAxisTitle = view.FrequencyAxisTitle;
        }
    }

    private static void OnRecreateRequired(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyHistogramPlotView view && view.Chart is not null)
        {
            view.Chart.Dispose();
            view.CreateChart();
        }
    }

    private void CreateChart()
    {
        Chart = new HistogramPlot(new HistogramChartOptions
        {
            Title = Title,
            Orientation = Orientation,
            ValueAxisTitle = ValueAxisTitle,
            FrequencyAxisTitle = FrequencyAxisTitle
        });
        Model = Chart.Model;
        Controller = Chart.Controller;
        Chart.IsActive = IsLoaded && IsVisible;
    }

    private void UpdateActiveState()
    {
        Chart.IsActive = IsLoaded && IsVisible;
    }
}
