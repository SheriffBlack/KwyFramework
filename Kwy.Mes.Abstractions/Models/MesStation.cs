namespace Kwy.Mes.Abstractions.Models;

public sealed record MesStation(
    string EquipmentId,
    string StationId,
    string? LineId = null,
    string? AreaId = null);
