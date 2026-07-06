namespace Kwy.Mes.Abstractions.Models;

public sealed record MesUnit(
    string SerialNumber,
    string? LotId = null,
    string? WorkOrderNo = null,
    string? ProductCode = null);
