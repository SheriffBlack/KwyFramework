using System.Collections.Concurrent;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Core;

/// <summary>
/// Stores application-owned device instances and provides lookup by id and capability.
/// </summary>
public sealed class DeviceRegistry : IDeviceRegistry
{
    private static readonly TimeSpan DeviceDisposeTimeout = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<string, IDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public IReadOnlyCollection<IDevice> Devices
    {
        get
        {
            ThrowIfDisposed();
            return devices.Values.ToArray();
        }
    }

    public void Add(IDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.DeviceId);

        if (devices.TryAdd(device.DeviceId, device))
        {
            return;
        }

        if (devices.TryGetValue(device.DeviceId, out IDevice? existing)
            && ReferenceEquals(existing, device))
        {
            return;
        }

        throw new InvalidOperationException($"A device with id '{device.DeviceId}' is already registered.");
    }

    public bool TryGetDevice(string deviceId, out IDevice device)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return devices.TryGetValue(deviceId, out device!);
    }

    public bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class
    {
        if (TryGetDevice(deviceId, out IDevice found) && found is TCapability typed)
        {
            device = typed;
            return true;
        }

        device = default!;
        return false;
    }

    public IDevice GetRequiredDevice(string deviceId)
    {
        return TryGetDevice(deviceId, out IDevice device)
            ? device
            : throw new KeyNotFoundException($"Device not found: {deviceId}");
    }

    public TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class
    {
        IDevice device = GetRequiredDevice(deviceId);
        return device as TCapability
            ?? throw new InvalidOperationException(
                $"Device {deviceId} is {device.GetType().FullName}, not {typeof(TCapability).FullName}.");
    }

    public IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class
    {
        ThrowIfDisposed();
        return devices.Values.OfType<TCapability>().ToArray();
    }

    public void Dispose()
    {
        if (!TryBeginDispose(out IDevice[] snapshot))
        {
            return;
        }

        Task[] disposeTasks = snapshot
            .Select(static device => DisposeDeviceSafelyAsync(device).AsTask())
            .ToArray();

        try
        {
            Task.WaitAll(disposeTasks, DeviceDisposeTimeout);
        }
        catch
        {
            // Disposal is best effort so one faulty driver cannot block shutdown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!TryBeginDispose(out IDevice[] snapshot))
        {
            return;
        }

        Task[] disposeTasks = snapshot
            .Select(static device => DisposeDeviceSafelyAsync(device).AsTask())
            .ToArray();

        try
        {
            await Task.WhenAll(disposeTasks).WaitAsync(DeviceDisposeTimeout).ConfigureAwait(false);
        }
        catch
        {
            // Disposal is best effort so one faulty driver cannot block shutdown.
        }
    }

    private bool TryBeginDispose(out IDevice[] snapshot)
    {
        if (disposed)
        {
            snapshot = Array.Empty<IDevice>();
            return false;
        }

        disposed = true;
        snapshot = devices.Values.ToArray();
        devices.Clear();
        return true;
    }

    private static async ValueTask DisposeDeviceSafelyAsync(IDevice device)
    {
        try
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Continue disposing the remaining devices.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
