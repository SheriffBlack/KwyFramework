namespace KwyTemplate.Flow.Models;

/// <summary>
/// 工站单个测试项的判定上下限。
/// </summary>
public sealed record StationMeasurementLimit(
    double? LowerLimit = null,
    double? UpperLimit = null,
    string? Unit = null);
