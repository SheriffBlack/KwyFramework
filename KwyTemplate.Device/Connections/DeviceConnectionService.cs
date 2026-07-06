using Kwy.Device.Abstractions;
using KwyTemplate.Device.Options;

namespace KwyTemplate.Device.Connections;

public sealed class DeviceConnectionService : IDeviceConnectionService
{
    private readonly IDeviceRegistry deviceRegistry;
    private readonly IDeviceConnectionOptionsStore optionsStore;
    private readonly IReadOnlyDictionary<string, IDeviceConnectionFactory> factories;
    private readonly SemaphoreSlim sync = new(1, 1);

    public DeviceConnectionService(
        IDeviceRegistry deviceRegistry,
        IDeviceConnectionOptionsStore optionsStore,
        IEnumerable<IDeviceConnectionFactory> factories)
    {
        this.deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.factories = factories?.ToDictionary(x => x.DeviceType, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(factories));
    }

    public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
        => await ConnectEntriesAsync(static entry => entry.Enabled, cancellationToken).ConfigureAwait(false);

    public async Task ConnectStartupDevicesAsync(CancellationToken cancellationToken = default)
        => await ConnectEntriesAsync(static entry => entry.Enabled && entry.ConnectOnStartup, cancellationToken).ConfigureAwait(false);

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var device in deviceRegistry.Devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (device.IsConnected)
            {
                await device.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeviceConnectionOptions options = await optionsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            DeviceConnectionEntry entry = FindEnabledEntry(options, deviceId);
            IDevice device = await EnsureDeviceAsync(entry, cancellationToken).ConfigureAwait(false);
            if (!device.IsConnected)
            {
                await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            sync.Release();
        }
    }

    public Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (!TryGetDevice<IDevice>(deviceId, out var device))
        {
            return Task.CompletedTask;
        }

        return device.IsConnected ? device.DisconnectAsync(cancellationToken) : Task.CompletedTask;
    }

    public async Task ConnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        string[] ids = NormalizeDeviceIds(deviceIds);
        if (ids.Length == 0)
        {
            return;
        }

        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeviceConnectionOptions options = await optionsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            foreach (string deviceId in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeviceConnectionEntry entry = FindEnabledEntry(options, deviceId);
                IDevice device = await EnsureDeviceAsync(entry, cancellationToken).ConfigureAwait(false);
                if (!device.IsConnected)
                {
                    await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            sync.Release();
        }
    }

    public async Task DisconnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        foreach (string deviceId in NormalizeDeviceIds(deviceIds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DisconnectDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConnectEntriesAsync(
        Func<DeviceConnectionEntry, bool> predicate,
        CancellationToken cancellationToken)
    {
        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeviceConnectionOptions options = await optionsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            foreach (DeviceConnectionEntry entry in options.Devices.Where(predicate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                IDevice device = await EnsureDeviceAsync(entry, cancellationToken).ConfigureAwait(false);
                if (!device.IsConnected)
                {
                    await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            sync.Release();
        }
    }

    private async Task<IDevice> EnsureDeviceAsync(
        DeviceConnectionEntry entry,
        CancellationToken cancellationToken)
    {
        IDeviceConnectionFactory factory = GetFactory(entry.DeviceType);
        if (deviceRegistry.TryGetDevice(entry.DeviceId, out var existing)
            && factory.IsSameDevice(existing, entry))
        {
            return existing;
        }

        await ReplaceDeviceAsync(entry.DeviceId, cancellationToken).ConfigureAwait(false);

        IDevice device = factory.Create(entry);
        deviceRegistry.AddOrUpdate(device);
        return device;
    }

    private async Task ReplaceDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (!TryGetDevice<IDevice>(deviceId, out var existing))
        {
            return;
        }

        if (existing.IsConnected)
        {
            await existing.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }

        deviceRegistry.Remove(deviceId, dispose: true);
    }

    private IDeviceConnectionFactory GetFactory(string deviceType)
    {
        if (factories.TryGetValue(deviceType, out IDeviceConnectionFactory? factory))
        {
            return factory;
        }

        throw new InvalidOperationException($"Device connection factory not found: {deviceType}.");
    }

    private static DeviceConnectionEntry FindEnabledEntry(DeviceConnectionOptions options, string deviceId)
    {
        DeviceConnectionEntry? entry = options.Find(deviceId);
        if (entry == null)
        {
            throw new KeyNotFoundException($"Device connection options not found: {deviceId}.");
        }

        if (!entry.Enabled)
        {
            throw new InvalidOperationException($"Device connection options are disabled: {deviceId}.");
        }

        return entry;
    }

    private bool TryGetDevice<TDevice>(string deviceId, out TDevice device)
        where TDevice : class
    {
        try
        {
            return deviceRegistry.TryGetDevice(deviceId, out device);
        }
        catch (ObjectDisposedException)
        {
            device = null!;
            return false;
        }
    }

    private static string[] NormalizeDeviceIds(IEnumerable<string> deviceIds)
    {
        return deviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
