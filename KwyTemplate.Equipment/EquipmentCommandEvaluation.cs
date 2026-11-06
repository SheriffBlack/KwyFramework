namespace KwyTemplate.Equipment;

/// <summary>单个整机命令的准入结果。</summary>
public sealed record EquipmentCommandEvaluation(
    bool IsAllowed,
    string? ReasonCode = null,
    string? Message = null)
{
    public static EquipmentCommandEvaluation Allowed { get; } = new(true);

    public static EquipmentCommandEvaluation Denied(string reasonCode, string message)
        => new(false, reasonCode, message);
}
