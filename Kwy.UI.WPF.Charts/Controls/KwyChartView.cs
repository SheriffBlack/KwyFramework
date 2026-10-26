using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Rendering;
using Kwy.UI.Charts.Abstractions.Updates;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 托管一个图表会话，而不将选定的渲染引擎暴露给应用程序视图。
/// </summary>
public class KwyChartView : ContentControl, IChartUpdateSink, IDisposable
{
    private const int MaxReplayUpdates = 100_000;

    public static readonly DependencyProperty BackendProperty = DependencyProperty.Register(
        nameof(Backend),
        typeof(IChartBackend<FrameworkElement>),
        typeof(KwyChartView),
        new PropertyMetadata(null, OnConfigurationChanged));

    public static readonly DependencyProperty DefinitionProperty = DependencyProperty.Register(
        nameof(Definition),
        typeof(ChartDefinition),
        typeof(KwyChartView),
        new PropertyMetadata(null, OnConfigurationChanged));

    private IChartSession<FrameworkElement>? session;
    private readonly List<ChartUpdate> replayUpdates = [];
    private bool disposed;

    public KwyChartView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public IChartBackend<FrameworkElement>? Backend
    {
        get => (IChartBackend<FrameworkElement>?)GetValue(BackendProperty);
        set => SetValue(BackendProperty, value);
    }

    public ChartDefinition? Definition
    {
        get => (ChartDefinition?)GetValue(DefinitionProperty);
        set => SetValue(DefinitionProperty, value);
    }

    public IChartSession? Session => session;

    public void Apply(ChartUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Remember(update);
        session?.Apply(update);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        IsVisibleChanged -= OnIsVisibleChanged;
        ReleaseSession();
        replayUpdates.Clear();
    }

    private static void OnConfigurationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyChartView)sender;
        if (!view.disposed)
            view.RecreateSession();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        EnsureSession();
        UpdateActiveState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (session is not null)
            session.IsActive = false;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args) => UpdateActiveState();

    private void RecreateSession()
    {
        ReleaseSession();
        EnsureSession();
        UpdateActiveState();
    }

    private void EnsureSession()
    {
        if (session is not null || Backend is null || Definition is null)
            return;

        session = Backend.Create(Definition);
        if (session.View is Control backendView)
        {
            BindingOperations.SetBinding(
                backendView,
                BackgroundProperty,
                new Binding(nameof(Background)) { Source = this });
        }
        Content = session.View;
        foreach (var update in replayUpdates)
            session.Apply(update);
    }

    private void UpdateActiveState()
    {
        if (session is not null)
            session.IsActive = IsLoaded && IsVisible;
    }

    private void ReleaseSession()
    {
        Content = null;
        session?.Dispose();
        session = null;
    }

    private void Remember(ChartUpdate update)
    {
        switch (update)
        {
            case ClearChartData:
                replayUpdates.RemoveAll(static item => item is AppendChartValue
                    or AppendChartValues
                    or SetPieSlice
                    or StartTimelineProcess
                    or StopTimelineProcess);
                break;
            case SetChartLimits:
                replayUpdates.RemoveAll(static item => item is SetChartLimits);
                replayUpdates.Add(update);
                break;
            case SetChartLabels:
                replayUpdates.RemoveAll(static item => item is SetChartLabels);
                replayUpdates.Add(update);
                break;
            default:
                replayUpdates.Add(update);
                TrimReplayUpdates();
                break;
        }
    }

    private void TrimReplayUpdates()
    {
        while (replayUpdates.Count > MaxReplayUpdates)
        {
            var dataIndex = replayUpdates.FindIndex(static item => item is AppendChartValue or AppendChartValues);
            replayUpdates.RemoveAt(dataIndex >= 0 ? dataIndex : 0);
        }
    }
}
