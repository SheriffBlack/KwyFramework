namespace KwyTemplate.Equipment;

/// <summary>整机命令的最终执行结果。</summary>
public sealed record EquipmentCommandResult(
    bool Succeeded,
    EquipmentState State,
    string? ReasonCode = null,
    string? Message = null);
