using KwyPecvd.Equipment.Commands;

namespace KwyPecvd.Equipment.Events;

/// <summary>
/// 表示 Equipment 已经发生的业务事实。
/// </summary>
/// <param name="EventId">事件唯一标识符。</param>
public sealed record EquipmentEvent(
    string EventId,
    string Code,
    DateTimeOffset OccurredAt,
    string EquipmentModuleId,
    EquipmentCommandContext? CommandContext,
    EquipmentCommandStatus? CommandStatus,
    string? ResultCode,
    string? Message);
