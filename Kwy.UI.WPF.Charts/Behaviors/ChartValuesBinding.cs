using System.Collections.Generic;
using System.Windows;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.Charts.Abstractions.Values;

namespace Kwy.UI.WPF.Charts.Behaviors;

/// <summary>
/// 将常见的采样点、历史数据和限值转换为通用图表更新。
/// </summary>
public static class ChartValuesBinding
{
    public static readonly DependencyProperty PointProperty = DependencyProperty.RegisterAttached(
        "Point",
        typeof(ChartValuePoint),
        typeof(ChartValuesBinding),
        new PropertyMetadata(null, OnPointChanged));

    public static readonly DependencyProperty PointsProperty = DependencyProperty.RegisterAttached(
        "Points",
        typeof(IEnumerable<ChartValuePoint>),
        typeof(ChartValuesBinding));

    public static readonly DependencyProperty LimitsProperty = DependencyProperty.RegisterAttached(
        "Limits",
        typeof(ChartLimitRange),
        typeof(ChartValuesBinding),
        new PropertyMetadata(null, OnLimitsChanged));

    public static readonly DependencyProperty ChannelKeyProperty = DependencyProperty.RegisterAttached(
        "ChannelKey",
        typeof(string),
        typeof(ChartValuesBinding),
        new PropertyMetadata("Default"));

    public static readonly DependencyProperty ResetKeyProperty = DependencyProperty.RegisterAttached(
        "ResetKey",
        typeof(object),
        typeof(ChartValuesBinding),
        new PropertyMetadata(null, OnResetKeyChanged));

    public static readonly DependencyProperty LowerLimitLabelProperty = RegisterLabel(
        "LowerLimitLabel",
        "Lower Limit");

    public static readonly DependencyProperty UpperLimitLabelProperty = RegisterLabel(
        "UpperLimitLabel",
        "Upper Limit");

    public static readonly DependencyProperty TargetValueLabelProperty = RegisterLabel(
        "TargetValueLabel",
        "Nominal");

    public static void SetPoint(DependencyObject element, ChartValuePoint? value) =>
        element.SetValue(PointProperty, value);

    public static ChartValuePoint? GetPoint(DependencyObject element) =>
        (ChartValuePoint?)element.GetValue(PointProperty);

    public static void SetPoints(DependencyObject element, IEnumerable<ChartValuePoint>? value) =>
        element.SetValue(PointsProperty, value);

    public static IEnumerable<ChartValuePoint>? GetPoints(DependencyObject element) =>
        (IEnumerable<ChartValuePoint>?)element.GetValue(PointsProperty);

    public static void SetLimits(DependencyObject element, ChartLimitRange? value) =>
        element.SetValue(LimitsProperty, value);

    public static ChartLimitRange? GetLimits(DependencyObject element) =>
        (ChartLimitRange?)element.GetValue(LimitsProperty);

    public static void SetChannelKey(DependencyObject element, string? value) =>
        element.SetValue(ChannelKeyProperty, value);

    public static string GetChannelKey(DependencyObject element) =>
        (string?)element.GetValue(ChannelKeyProperty) ?? "Default";

    public static void SetResetKey(DependencyObject element, object? value) =>
        element.SetValue(ResetKeyProperty, value);

    public static object? GetResetKey(DependencyObject element) =>
        element.GetValue(ResetKeyProperty);

    public static void SetLowerLimitLabel(DependencyObject element, string? value) =>
        element.SetValue(LowerLimitLabelProperty, value);

    public static string GetLowerLimitLabel(DependencyObject element) =>
        (string?)element.GetValue(LowerLimitLabelProperty) ?? "Lower Limit";

    public static void SetUpperLimitLabel(DependencyObject element, string? value) =>
        element.SetValue(UpperLimitLabelProperty, value);

    public static string GetUpperLimitLabel(DependencyObject element) =>
        (string?)element.GetValue(UpperLimitLabelProperty) ?? "Upper Limit";

    public static void SetTargetValueLabel(DependencyObject element, string? value) =>
        element.SetValue(TargetValueLabelProperty, value);

    public static string GetTargetValueLabel(DependencyObject element) =>
        (string?)element.GetValue(TargetValueLabelProperty) ?? "Nominal";

    private static DependencyProperty RegisterLabel(string name, string defaultValue) =>
        DependencyProperty.RegisterAttached(
            name,
            typeof(string),
            typeof(ChartValuesBinding),
            new PropertyMetadata(defaultValue, OnLabelsChanged));

    private static void OnPointChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is ChartValuePoint point)
        {
            PushPoint(sender, point);
            return;
        }

        ChartUpdatesBinding.TryPush(sender, ClearChartData.Instance);
    }

    private static void OnLimitsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        PushLimits(sender, args.NewValue as ChartLimitRange);

    private static void OnResetKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        Replay(sender);

    private static void OnLabelsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        PushLabels(sender);

    private static void Replay(DependencyObject target)
    {
        ChartUpdatesBinding.TryPush(target, ClearChartData.Instance);
        PushLimits(target, GetLimits(target));
        PushLabels(target);

        foreach (var point in GetPoints(target) ?? [])
            PushPoint(target, point);
    }

    private static void PushPoint(DependencyObject target, ChartValuePoint point)
    {
        if (!double.IsFinite(point.Value))
            return;

        ChartUpdatesBinding.TryPush(target, new AppendChartValue(
            GetChannelKey(target),
            point.Value,
            point.IsPass,
            point.Timestamp));
    }

    private static void PushLimits(DependencyObject target, ChartLimitRange? limits) =>
        ChartUpdatesBinding.TryPush(target, new SetChartLimits(
            limits?.LowerLimit,
            limits?.UpperLimit,
            limits?.TargetValue));

    private static void PushLabels(DependencyObject target) =>
        ChartUpdatesBinding.TryPush(target, new SetChartLabels(
            GetLowerLimitLabel(target),
            GetUpperLimitLabel(target),
            GetTargetValueLabel(target)));
}
