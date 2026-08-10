using System.Collections.Generic;
using System.Windows;
using Kwy.ComponentModel;
using Kwy.UI.WPF.Charts.OxyPlot.Controls;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Behaviors;

public static class ChartValuePushBehavior
{
    private static readonly List<WeakReference<DependencyObject>> localizedCharts = [];

    static ChartValuePushBehavior()
        => PropertyMetadataLocalization.Changed += (_, _) => RefreshLocalizedCharts();

    public static readonly DependencyProperty SampleProperty =
        DependencyProperty.RegisterAttached(
            "Sample",
            typeof(ChartValueSample),
            typeof(ChartValuePushBehavior),
            new PropertyMetadata(null, OnSampleChanged));

    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.RegisterAttached(
            "Samples",
            typeof(IEnumerable<ChartValueSample>),
            typeof(ChartValuePushBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LimitsProperty =
        DependencyProperty.RegisterAttached(
            "Limits",
            typeof(ChartLimitSet),
            typeof(ChartValuePushBehavior),
            new PropertyMetadata(null, OnLimitsChanged));

    public static readonly DependencyProperty ParameterKeyProperty =
        DependencyProperty.RegisterAttached(
            "ParameterKey",
            typeof(string),
            typeof(ChartValuePushBehavior),
            new PropertyMetadata(null, OnParameterKeyChanged));

    public static void SetSample(DependencyObject element, ChartValueSample? value)
        => element.SetValue(SampleProperty, value);

    public static ChartValueSample? GetSample(DependencyObject element)
        => (ChartValueSample?)element.GetValue(SampleProperty);

    public static void SetSamples(DependencyObject element, IEnumerable<ChartValueSample>? value)
        => element.SetValue(SamplesProperty, value);

    public static IEnumerable<ChartValueSample>? GetSamples(DependencyObject element)
        => (IEnumerable<ChartValueSample>?)element.GetValue(SamplesProperty);

    public static void SetLimits(DependencyObject element, ChartLimitSet? value)
        => element.SetValue(LimitsProperty, value);

    public static ChartLimitSet? GetLimits(DependencyObject element)
        => (ChartLimitSet?)element.GetValue(LimitsProperty);

    public static void SetParameterKey(DependencyObject element, string? value)
        => element.SetValue(ParameterKeyProperty, value);

    public static string? GetParameterKey(DependencyObject element)
        => (string?)element.GetValue(ParameterKeyProperty);

    private static void OnParameterKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        TrackLocalizedChart(d);
        ClearData(d);

        ChartLimitSet? limits = GetLimits(d);
        PushLimits(d, limits?.LowerLimit, limits?.UpperLimit, limits?.TargetValue);

        foreach (ChartValueSample sample in GetSamples(d) ?? System.Array.Empty<ChartValueSample>())
        {
            PushSample(d, sample);
        }
    }

    private static void OnSampleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ChartValueSample sample)
        {
            PushSample(d, sample);
        }
    }

    private static void PushSample(DependencyObject d, ChartValueSample sample)
    {
        if (double.IsNaN(sample.Value) || double.IsInfinity(sample.Value))
        {
            return;
        }

        switch (d)
        {
            case KwyScatterTrendPlotView scatter:
                scatter.AddValue(sample.Value, sample.IsPass);
                break;

            case KwyHistogramPlotView histogram:
                histogram.AddValue(sample.Value);
                break;
        }
    }

    private static void OnLimitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        TrackLocalizedChart(d);
        if (e.NewValue is not ChartLimitSet limits)
        {
            PushLimits(d, null, null, null);
            return;
        }

        PushLimits(d, limits.LowerLimit, limits.UpperLimit, limits.TargetValue);
    }

    private static void PushLimits(DependencyObject d, double? lowerLimit, double? upperLimit, double? targetValue)
    {
        switch (d)
        {
            case KwyScatterTrendPlotView scatter:
                scatter.SetLimits(lowerLimit, upperLimit, targetValue);
                break;

            case KwyHistogramPlotView histogram:
                histogram.SetLimits(lowerLimit, upperLimit, targetValue);
                break;
        }
    }

    private static void TrackLocalizedChart(DependencyObject chart)
    {
        if (chart is not KwyScatterTrendPlotView && chart is not KwyHistogramPlotView)
        {
            return;
        }

        lock (localizedCharts)
        {
            localizedCharts.RemoveAll(static item => !item.TryGetTarget(out _));
            if (!localizedCharts.Exists(item => item.TryGetTarget(out DependencyObject? target) && ReferenceEquals(target, chart)))
            {
                localizedCharts.Add(new WeakReference<DependencyObject>(chart));
            }
        }

        ApplyLocalizedLimitLabels(chart);
    }

    private static void RefreshLocalizedCharts()
    {
        DependencyObject[] charts;
        lock (localizedCharts)
        {
            localizedCharts.RemoveAll(static item => !item.TryGetTarget(out _));
            charts = localizedCharts
                .Select(static item => item.TryGetTarget(out DependencyObject? target) ? target : null)
                .OfType<DependencyObject>()
                .ToArray();
        }

        foreach (DependencyObject chart in charts)
        {
            if (chart.Dispatcher.HasShutdownStarted)
            {
                continue;
            }

            chart.Dispatcher.BeginInvoke(() => ApplyLocalizedLimitLabels(chart));
        }
    }

    private static void ApplyLocalizedLimitLabels(DependencyObject chart)
    {
        string upper = GetResourceText("Home.Chart.UpperLimit", "Upper Limit");
        string lower = GetResourceText("Home.Chart.LowerLimit", "Lower Limit");
        string target = GetResourceText("Home.Chart.TargetValue", "Nominal");

        switch (chart)
        {
            case KwyScatterTrendPlotView scatter:
                scatter.SetCurrentValue(KwyScatterTrendPlotView.UpperLimitLabelProperty, upper);
                scatter.SetCurrentValue(KwyScatterTrendPlotView.LowerLimitLabelProperty, lower);
                scatter.SetCurrentValue(KwyScatterTrendPlotView.TargetValueLabelProperty, target);
                scatter.Chart.RefreshLimitLabels();
                break;

            case KwyHistogramPlotView histogram:
                histogram.SetCurrentValue(KwyHistogramPlotView.UpperLimitLabelProperty, upper);
                histogram.SetCurrentValue(KwyHistogramPlotView.LowerLimitLabelProperty, lower);
                histogram.SetCurrentValue(KwyHistogramPlotView.TargetValueLabelProperty, target);
                histogram.Chart.RefreshLimitLabels();
                break;
        }
    }

    private static string GetResourceText(string key, string fallback)
    {
        string? text = Application.Current?.TryFindResource(key)?.ToString();
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private static void ClearData(DependencyObject d)
    {
        switch (d)
        {
            case KwyScatterTrendPlotView scatter:
                scatter.ClearData();
                break;

            case KwyHistogramPlotView histogram:
                histogram.ClearData();
                break;
        }
    }
}
