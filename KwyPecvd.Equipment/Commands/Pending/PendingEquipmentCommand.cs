namespace KwyPecvd.Equipment.Commands;


/// <summary>
/// 表示已接受但尚未完成的 Equipment Command。
/// </summary>
public sealed record PendingEquipmentCommand(
    EquipmentCommandContext Context,
    string CommandTypeCode,
    string EquipmentModuleId,
    DateTimeOffset TrackedAt)
{
    public string RequestId => Context.RequestId;
}