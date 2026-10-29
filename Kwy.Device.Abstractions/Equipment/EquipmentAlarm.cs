namespace Kwy.Device.Abstractions.Equipment;

public enum EquipmentAlarmState
{
    Active,
    Cleared,
    Acknowledged
}

/// <summary>表示具有稳定代码和生命周期的整机报警。</summary>
public sealed record EquipmentAlarm(
    string Code,
    string Message,
    EquipmentEventSeverity Severity = EquipmentEventSeverity.Error,
    EquipmentAlarmState State = EquipmentAlarmState.Active,
    string? Source = null,
    DateTimeOffset? Timestamp = null,
    IReadOnlyDictionary<string, string>? Properties = null);
