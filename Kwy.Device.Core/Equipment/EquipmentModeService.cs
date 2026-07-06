using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentModeService : IEquipmentModeService
{
    private readonly SemaphoreSlim modeSemaphore = new(1, 1);

    public EquipmentMode CurrentMode { get; private set; } = EquipmentMode.Unknown;

    public event EventHandler<EquipmentModeChangedEventArgs>? ModeChanged;

    public async Task SetModeAsync(
        EquipmentMode mode,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await modeSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (CurrentMode == mode)
            {
                return;
            }

            var previous = CurrentMode;
            CurrentMode = mode;
            ModeChanged?.Invoke(this, new EquipmentModeChangedEventArgs(previous, mode, reason));
        }
        finally
        {
            modeSemaphore.Release();
        }
    }
}
