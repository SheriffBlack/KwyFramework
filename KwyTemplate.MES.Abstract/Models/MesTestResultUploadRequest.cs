namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesTestResultUploadRequest(
    MesRequestContext Context,
    string WorkOrderNo,
    string UnitId,
    string StationName,
    bool Passed,
    DateTimeOffset Time,
    IReadOnlyList<MesMeasurementResult> Measurements,
    string? ProductNo = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    MesParameterBag? Parameters = null);