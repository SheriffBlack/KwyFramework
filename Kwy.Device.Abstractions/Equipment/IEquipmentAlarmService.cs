namespace Kwy.Device.Abstractions.Equipment;

/// <summary>管理当前活动的整机报警。</summary>
public interface IEquipmentAlarmService
{
    IReadOnlyCollection<EquipmentAlarm> ActiveAlarms { get; }

    Task RaiseAsync(EquipmentAlarm alarm, CancellationToken cancellationToken = default);

    Task ClearAsync(string code, string? reason = null, CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(string code, string operatorName, CancellationToken cancellationToken = default);
}
