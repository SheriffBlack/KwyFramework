using System.Windows;
using System.Windows.Media;
using Kwy.UI.WPF.Charts.Abstractions;

namespace Kwy.UI.WPF.Charts.OxyPlot.Controls;

public class KwyScatterTrendPlotView : global::OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(KwyScatterTrendPlotView), new PropertyMetadata(string.Empty, OnOptionChanged));

    public static readonly DependencyProperty SampleAxisTitleProperty =
        DependencyProperty.Register(nameof(SampleAxisTitle), typeof(string), typeof(KwyScatterTrendPlotView), new PropertyMetadata("Sample", OnOptionChanged));

    public static readonly DependencyProperty ValueAxisTitleProperty =
        DependencyProperty.Register(nameof(ValueAxisTitle), typeof(string), typeof(KwyScatterTrendPlotView), new PropertyMetadata("Value", OnOptionChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(PlotOrientation), typeof(KwyScatterTrendPlotView), new PropertyMetadata(PlotOrientation.Horizontal, OnRecreateRequired));

    public KwyScatterTrendPlotView()
    {
        CreateChart();
        Loaded += (_, _) => UpdateActiveState();
        IsVisibleChanged += (_, _) => UpdateActiveState();
        Unloaded += (_, _) => Chart.IsActive = false;
    }

    public ScatterTrendPlot Chart { get; private set; } = null!;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SampleAxisTitle
    {
        get => (string)GetValue(SampleAxisTitleProperty);
        set => SetValue(SampleAxisTitleProperty, value);
    }

    public string ValueAxisTitle
    {
        get => (string)GetValue(ValueAxisTitleProperty);
        set => SetValue(ValueAxisTitleProperty, value);
    }

    public PlotOrientation Orientation
    {
        get => (PlotOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public void AddChannel(string name, Color? color = null) => Chart.AddChannel(name, color);

    public void AddValue(double value) => Chart.AddValue(value);

    public void SetLimits(double? lower, double? upper, double? target) => Chart.SetLimits(lower, upper, target);

    public void ClearData() => Chart.ClearData();

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyScatterTrendPlotView view && view.Chart is not null)
        {
            view.Chart.Title = view.Title;
            view.Chart.SampleAxisTitle = view.SampleAxisTitle;
            view.Chart.ValueAxisTitle = view.ValueAxisTitle;
        }
    }

    private static void OnRecreateRequired(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyScatterTrendPlotView view && view.Chart is not null)
        {
            view.Chart.Dispose();
            view.CreateChart();
        }
    }

    private void CreateChart()
    {
        Chart = new ScatterTrendPlot(new ScatterTrendChartOptions
        {
            Title = Title,
            Orientation = Orientation,
            SampleAxisTitle = SampleAxisTitle,
            ValueAxisTitle = ValueAxisTitle
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
