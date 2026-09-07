namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesRequestContext(
    string MachineId,
    string MachineName,
    string OperatorId,
    string? StationName = null,
    string? ProductNo = null,
    string? WorkOrderNo = null,
    IReadOnlyDictionary<string, string>? Extra = null);