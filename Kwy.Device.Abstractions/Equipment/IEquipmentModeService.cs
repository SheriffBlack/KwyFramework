namespace Kwy.Device.Abstractions.Equipment;

public interface IEquipmentModeService
{
    EquipmentMode CurrentMode { get; }

    event EventHandler<EquipmentModeChangedEventArgs>? ModeChanged;

    Task SetModeAsync(
        EquipmentMode mode,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
