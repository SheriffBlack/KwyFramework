using System.Windows;
using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.OxyPlot.Controls;

public class KwyPiePlotView : global::OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(KwyPiePlotView), new PropertyMetadata(string.Empty, OnOptionChanged));

    public KwyPiePlotView()
    {
        Chart = new PiePlot(Title);
        Model = Chart.Model;
        Controller = Chart.Controller;
        Chart.IsActive = false;
        Loaded += (_, _) => UpdateActiveState();
        IsVisibleChanged += (_, _) => UpdateActiveState();
        Unloaded += (_, _) => Chart.IsActive = false;
    }

    public PiePlot Chart { get; }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public void AddOrUpdateSlice(string key, string label, double value, Color? color = null, bool isExploded = false)
        => Chart.AddOrUpdateSlice(key, label, value, color, isExploded);

    public void ClearData() => Chart.ClearData();

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyPiePlotView view && view.Chart is not null)
        {
            view.Chart.Title = view.Title;
        }
    }

    private void UpdateActiveState()
    {
        Chart.IsActive = IsLoaded && IsVisible;
    }
}
