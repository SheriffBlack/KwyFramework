namespace Kwy.Mes.Abstractions.Models;

public sealed record MesTestResult(
    MesUnit Unit,
    MesStation Station,
    bool Passed,
    DateTimeOffset Time,
    IReadOnlyList<MesMeasurement> Measurements,
    string? ErrorCode = null,
    string? ErrorMessage = null);
