namespace Kwy.Device.Abstractions.Equipment;

public interface IEquipmentEventSink
{
    Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default);
}

public interface IAlarmService
{
    IReadOnlyCollection<EquipmentAlarm> ActiveAlarms { get; }

    Task RaiseAsync(EquipmentAlarm alarm, CancellationToken cancellationToken = default);

    Task ClearAsync(string code, string? reason = null, CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(string code, string operatorName, CancellationToken cancellationToken = default);
}

public interface IAuditTrail
{
    Task RecordAsync(EquipmentAuditRecord record, CancellationToken cancellationToken = default);
}
