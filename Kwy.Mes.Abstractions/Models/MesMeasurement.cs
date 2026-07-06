namespace Kwy.Mes.Abstractions.Models;

public sealed record MesMeasurement(
    string Name,
    double Value,
    string? Unit = null,
    double? LowerLimit = null,
    double? UpperLimit = null,
    bool? Passed = null);
