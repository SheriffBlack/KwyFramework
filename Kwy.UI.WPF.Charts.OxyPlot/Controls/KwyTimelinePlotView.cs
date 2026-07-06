using System.Windows;
using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.OxyPlot.Controls;

public class KwyTimelinePlotView : global::OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(KwyTimelinePlotView), new PropertyMetadata(string.Empty, OnOptionChanged));

    public static readonly DependencyProperty TimeAxisTitleProperty =
        DependencyProperty.Register(nameof(TimeAxisTitle), typeof(string), typeof(KwyTimelinePlotView), new PropertyMetadata("Time (ms)", OnOptionChanged));

    public static readonly DependencyProperty ProcessAxisTitleProperty =
        DependencyProperty.Register(nameof(ProcessAxisTitle), typeof(string), typeof(KwyTimelinePlotView), new PropertyMetadata("Process", OnOptionChanged));

    public KwyTimelinePlotView()
    {
        Chart = new TimelinePlot(new Kwy.UI.WPF.Charts.Abstractions.TimelineChartOptions
        {
            Title = Title,
            TimeAxisTitle = TimeAxisTitle,
            ProcessAxisTitle = ProcessAxisTitle
        });
        Model = Chart.Model;
        Controller = Chart.Controller;
        Chart.IsActive = false;
        Loaded += (_, _) => UpdateActiveState();
        IsVisibleChanged += (_, _) => UpdateActiveState();
        Unloaded += (_, _) => Chart.IsActive = false;
    }

    public TimelinePlot Chart { get; }

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

    public string ProcessAxisTitle
    {
        get => (string)GetValue(ProcessAxisTitleProperty);
        set => SetValue(ProcessAxisTitleProperty, value);
    }

    public void AddChannel(string key, string name, Color? color = null) => Chart.AddChannel(key, name, color);

    public void StartProcess(string key, string label = "") => Chart.StartProcess(key, label);

    public void StopProcess(string key) => Chart.StopProcess(key);

    public double AddStep(string key, double startTime, double duration, string label = "", Color? color = null)
        => Chart.AddStep(key, startTime, duration, label, color);

    public double AddSequentialStep(string key, double durationMs, string label = "", Color? color = null)
        => Chart.AddSequentialStep(key, durationMs, label, color);

    public void ClearData() => Chart.ClearData();

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyTimelinePlotView view && view.Chart is not null)
        {
            view.Chart.Title = view.Title;
            view.Chart.TimeAxisTitle = view.TimeAxisTitle;
            view.Chart.ProcessAxisTitle = view.ProcessAxisTitle;
        }
    }

    private void UpdateActiveState()
    {
        Chart.IsActive = IsLoaded && IsVisible;
    }
}
