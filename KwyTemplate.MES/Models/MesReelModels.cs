namespace KwyTemplate.MES.Models;

public sealed record MesReelScanRequest(
    MesRequestContext Context,
    string WorkOrderNo,
    string ReelId,
    string? Barcode = null);

public sealed record MesReelScanResult(
    bool Accepted,
    string? ReelId,
    string Message,
    string? MatNo = null,
    string? TpNo = null,
    MesParameterBag? Parameters = null);