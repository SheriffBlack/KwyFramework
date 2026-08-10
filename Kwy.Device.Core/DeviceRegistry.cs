using System.Collections.Concurrent;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core;

public sealed class DeviceRegistry : IDeviceRegistry
{
    private static readonly TimeSpan DeviceDisposeTimeout = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<string, IDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DeviceSubscription> subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEquipmentEventSink? eventSink;
    private readonly IAlarmService? alarmService;
    private bool disposed;

    public IReadOnlyCollection<IDevice> Devices => devices.Values.ToArray();

    public DeviceRegistry()
    {
    }

    public DeviceRegistry(IEquipmentEventSink eventSink, IAlarmService alarmService)
    {
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        this.alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
    }

    public bool TryAdd(IDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device);
        if (!devices.TryAdd(device.DeviceId, device))
        {
            return false;
        }

        AttachDeviceEvents(device);
        return true;
    }

    public void AddOrUpdate(IDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device);
        devices.AddOrUpdate(device.DeviceId, device, (_, oldDevice) =>
        {
            if (!ReferenceEquals(oldDevice, device))
            {
                DetachDeviceEvents(oldDevice);
                oldDevice.Dispose();
            }

            return device;
        });

        AttachDeviceEvents(device);
    }

    public bool Remove(string deviceId, bool dispose = false)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id cannot be empty.", nameof(deviceId));
        }

        if (!devices.TryRemove(deviceId, out var device))
        {
            return false;
        }

        DetachDeviceEvents(device);
        if (dispose)
        {
            device.Dispose();
        }

        return true;
    }

    public bool TryGetDevice(string deviceId, out IDevice device)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id cannot be empty.", nameof(deviceId));
        }

        return devices.TryGetValue(deviceId, out device!);
    }

    public bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class
    {
        if (TryGetDevice(deviceId, out var found) && found is TCapability typed)
        {
            device = typed;
            return true;
        }

        device = default!;
        return false;
    }

    public IDevice GetRequiredDevice(string deviceId)
    {
        return TryGetDevice(deviceId, out var device)
            ? device
            : throw new KeyNotFoundException($"Device not found: {deviceId}");
    }

    public TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class
    {
        var device = GetRequiredDevice(deviceId);
        return device as TCapability
            ?? throw new InvalidOperationException($"Device {deviceId} is {device.GetType().FullName}, not {typeof(TCapability).FullName}.");
    }

    public IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class
    {
        ThrowIfDisposed();
        return devices.Values.OfType<TCapability>().ToArray();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        IDevice[] snapshot = devices.Values.ToArray();
        foreach (var device in snapshot)
        {
            DetachDeviceEvents(device);
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
        }

        devices.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        IDevice[] snapshot = devices.Values.ToArray();
        foreach (var device in snapshot)
        {
            DetachDeviceEvents(device);
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
        }

        devices.Clear();
    }

    private static async ValueTask DisposeDeviceSafelyAsync(IDevice device)
    {
        try
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(DeviceRegistry));
        }
    }

    private void AttachDeviceEvents(IDevice device)
    {
        if (eventSink is null && alarmService is null)
        {
            return;
        }

        DetachDeviceEvents(device);

        EventHandler<ConnectionStateChangedEventArgs> stateHandler = (_, args) =>
            PublishStateChangedAsync(device, args).Forget();
        EventHandler<ErrorOccurredEventArgs> errorHandler = (_, args) =>
            PublishDeviceErrorAsync(device, args).Forget();
        EventHandler<DeviceOperationEventArgs> operationHandler = (_, args) =>
            PublishDeviceOperationAsync(device, args).Forget();

        device.StateChanged += stateHandler;
        device.ErrorOccurred += errorHandler;
        device.OperationOccurred += operationHandler;
        subscriptions[device.DeviceId] = new DeviceSubscription(stateHandler, errorHandler, operationHandler);
    }

    private void DetachDeviceEvents(IDevice device)
    {
        if (!subscriptions.TryRemove(device.DeviceId, out var subscription))
        {
            return;
        }

        device.StateChanged -= subscription.StateChanged;
        device.ErrorOccurred -= subscription.ErrorOccurred;
        device.OperationOccurred -= subscription.OperationOccurred;
    }

    private async Task PublishStateChangedAsync(IDevice device, ConnectionStateChangedEventArgs args)
    {
        if (eventSink is not null)
        {
            await eventSink.PublishAsync(new EquipmentEvent(
                "DeviceStateChanged",
                $"Device {device.DeviceId} state changed: {args.PreviousState} -> {args.CurrentState}.",
                args.CurrentState == ConnectionState.Error ? EquipmentEventSeverity.Error : EquipmentEventSeverity.Information,
                EquipmentEventKind.Event,
                device.DeviceId,
                Properties: new Dictionary<string, string>
                {
                    ["DeviceName"] = device.DeviceName,
                    ["PreviousState"] = args.PreviousState.ToString(),
                    ["CurrentState"] = args.CurrentState.ToString()
                })).ConfigureAwait(false);
        }

        if (alarmService is null)
        {
            return;
        }

        string alarmCode = GetConnectionAlarmCode(device.DeviceId);
        if (args.CurrentState == ConnectionState.Error)
        {
            await alarmService.RaiseAsync(new EquipmentAlarm(
                alarmCode,
                $"Device {device.DeviceId} entered Error state.",
                EquipmentEventSeverity.Error,
                Source: device.DeviceId)).ConfigureAwait(false);
        }
        else if (args.CurrentState == ConnectionState.Connected)
        {
            await alarmService.ClearAsync(alarmCode, $"Device {device.DeviceId} reconnected.").ConfigureAwait(false);
            await alarmService.ClearAsync(GetErrorAlarmCode(device.DeviceId), $"Device {device.DeviceId} returned to Connected.").ConfigureAwait(false);
        }
    }

    private async Task PublishDeviceErrorAsync(IDevice device, ErrorOccurredEventArgs args)
    {
        if (eventSink is not null)
        {
            await eventSink.PublishAsync(new EquipmentEvent(
                "DeviceError",
                args.Message,
                EquipmentEventSeverity.Error,
                EquipmentEventKind.Event,
                device.DeviceId,
                Properties: new Dictionary<string, string>
                {
                    ["DeviceName"] = device.DeviceName,
                    ["ExceptionType"] = args.Exception.GetType().FullName ?? args.Exception.GetType().Name,
                    ["Exception"] = args.Exception.ToString()
                })).ConfigureAwait(false);
        }

        if (alarmService is not null)
        {
            await alarmService.RaiseAsync(new EquipmentAlarm(
                GetErrorAlarmCode(device.DeviceId),
                args.Message,
                EquipmentEventSeverity.Error,
                Source: device.DeviceId)).ConfigureAwait(false);
        }
    }
    private async Task PublishDeviceOperationAsync(IDevice device, DeviceOperationEventArgs args)
    {
        if (eventSink is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DeviceName"] = device.DeviceName,
            ["DeviceType"] = device.GetType().FullName ?? device.GetType().Name,
            ["OperationKind"] = args.Kind.ToString(),
            ["OperationName"] = args.OperationName,
            ["IsSuccess"] = args.IsSuccess.ToString()
        };

        if (args.Properties != null)
        {
            foreach (var pair in args.Properties)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        if (args.Exception != null)
        {
            properties["ExceptionType"] = args.Exception.GetType().FullName ?? args.Exception.GetType().Name;
            properties["Exception"] = args.Exception.ToString();
        }

        await eventSink.PublishAsync(new EquipmentEvent(
            "DeviceOperation",
            args.Message,
            args.IsSuccess ? EquipmentEventSeverity.Information : EquipmentEventSeverity.Error,
            EquipmentEventKind.Operation,
            device.DeviceId,
            Properties: properties)).ConfigureAwait(false);
    }

    private static string GetConnectionAlarmCode(string deviceId)
        => $"DEVICE.{deviceId}.CONNECTION";

    private static string GetErrorAlarmCode(string deviceId)
        => $"DEVICE.{deviceId}.ERROR";

    private sealed record DeviceSubscription(
        EventHandler<ConnectionStateChangedEventArgs> StateChanged,
        EventHandler<ErrorOccurredEventArgs> ErrorOccurred,
        EventHandler<DeviceOperationEventArgs> OperationOccurred);
}

internal static class DeviceRegistryTaskExtensions
{
    public static void Forget(this Task task)
    {
        _ = task.ContinueWith(
            _ => { },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}




