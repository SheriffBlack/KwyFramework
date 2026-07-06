namespace Kwy.Device.Abstractions.Equipment;

public enum EquipmentEventSeverity
{
    Trace,
    Information,
    Warning,
    Error,
    Critical
}

public enum EquipmentEventKind
{
    Event,
    Alarm,
    Audit,
    Recovery,
    Operation
}

public enum EquipmentAlarmState
{
    Active,
    Cleared,
    Acknowledged
}

public sealed record EquipmentEvent(
    string Code,
    string Message,
    EquipmentEventSeverity Severity = EquipmentEventSeverity.Information,
    EquipmentEventKind Kind = EquipmentEventKind.Event,
    string? Source = null,
    DateTimeOffset? Timestamp = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record EquipmentAlarm(
    string Code,
    string Message,
    EquipmentEventSeverity Severity = EquipmentEventSeverity.Error,
    EquipmentAlarmState State = EquipmentAlarmState.Active,
    string? Source = null,
    DateTimeOffset? Timestamp = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record EquipmentAuditRecord(
    string Action,
    string Operator,
    string Message,
    string? Source = null,
    DateTimeOffset? Timestamp = null,
    IReadOnlyDictionary<string, string>? Properties = null);
