namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 包装一条设备业务命令及其调用上下文。
/// </summary>
public sealed record EquipmentCommandEnvelope(
    string RequestId,
    EquipmentCommandSource Source,
    string Requester,
    DateTimeOffset RequestedAt,
    IEquipmentCommand Command);
