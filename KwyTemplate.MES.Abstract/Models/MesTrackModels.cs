namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesTrackRequest(
    MesRequestContext Context,
    string UnitId,
    string WorkOrderNo);

public sealed record MesTrackOutRequest(
    MesRequestContext Context,
    string UnitId,
    string WorkOrderNo,
    bool Passed,
    IReadOnlyList<MesMeasurementResult> Measurements,
    int? OutputQuantity = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record MesTrackResult(
    bool Accepted,
    string Message,
    MesParameterBag? Parameters = null);