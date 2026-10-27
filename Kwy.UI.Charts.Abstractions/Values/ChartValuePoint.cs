namespace Kwy.UI.Charts.Abstractions.Values;

/// <summary>
/// 与具体图表引擎和 UI 框架无关的图表采样点。
/// </summary>
public sealed record ChartValuePoint(
    long Sequence,
    double Value,
    bool? IsPass = null,
    double? Timestamp = null);

/// <summary>
/// 图表的下限、上限和目标值。
/// </summary>
public sealed record ChartLimitRange(
    double? LowerLimit,
    double? UpperLimit,
    double? TargetValue = null);
