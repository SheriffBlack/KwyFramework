using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.PLCs.Hsl;

public sealed class HslPlcStateSynchronizer : IDeviceStateParticipant
{
    private readonly HslPlcDevice device;
    private readonly HslPlcRuntimeOptions options;

    public HslPlcStateSynchronizer(HslPlcDevice device, HslPlcRuntimeOptions options)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string DeviceId => device.DeviceId;

    public async Task<DeviceSyncResult> SyncStateAsync(CancellationToken cancellationToken = default)
    {
        if (!device.IsConnected)
        {
            return new DeviceSyncResult(
                DeviceSyncState.Offline,
                Array.Empty<DeviceSyncItem>(),
                "HSL PLC is not connected.");
        }

        var items = new List<DeviceSyncItem>(options.StatePoints.Count + 3)
        {
            new(nameof(device.DeviceId), device.DeviceId),
            new(nameof(device.DeviceModel), device.DeviceModel),
            new(nameof(device.State), device.State.ToString())
        };

        try
        {
            foreach (var point in options.StatePoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string value = await HslPlcValueReader.ReadAsStringAsync(device, point, cancellationToken);
                items.Add(new DeviceSyncItem(point.Name, value));
            }

            return DeviceSyncResult.Synchronized(items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DeviceSyncResult(DeviceSyncState.Failed, items, ex.Message);
        }
    }
}
