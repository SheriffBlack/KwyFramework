using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;
using Microsoft.Extensions.Logging;

namespace Kwy.Device.Core.Equipment;

public sealed class DeviceEventForwarder : IDeviceEventForwarder, IDisposable
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, DeviceSubscription> subscriptions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IEquipmentEventSink eventSink;
    private readonly IEquipmentAlarmService alarmService;
    private readonly ILogger<DeviceEventForwarder> logger;
    private bool disposed;

    public DeviceEventForwarder(
        IEquipmentEventSink eventSink,
        IEquipmentAlarmService alarmService,
        ILogger<DeviceEventForwarder> logger)
    {
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        this.alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Attach(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            DetachCore(device.DeviceId);

            EventHandler<ConnectionStateChangedEventArgs> stateHandler = (_, args) =>
                Observe(PublishStateChangedAsync(device, args));
            EventHandler<ErrorOccurredEventArgs> errorHandler = (_, args) =>
                Observe(PublishDeviceErrorAsync(device, args));
            EventHandler<DeviceOperationEventArgs> operationHandler = (_, args) =>
                Observe(PublishDeviceOperationAsync(device, args));

            device.StateChanged += stateHandler;
            device.ErrorOccurred += errorHandler;
            device.OperationOccurred += operationHandler;
            subscriptions.Add(
                device.DeviceId,
                new DeviceSubscription(device, stateHandler, errorHandler, operationHandler));
        }
    }

    public void Detach(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (syncRoot)
        {
            if (subscriptions.TryGetValue(device.DeviceId, out DeviceSubscription? subscription)
                && ReferenceEquals(subscription.Device, device))
            {
                DetachCore(device.DeviceId);
            }
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (DeviceSubscription subscription in subscriptions.Values)
            {
                Unsubscribe(subscription);
            }

            subscriptions.Clear();
        }
    }

    private async Task PublishStateChangedAsync(IDevice device, ConnectionStateChangedEventArgs args)
    {
        var properties = new Dictionary<string, string>
        {
            ["DeviceName"] = device.DeviceName,
            ["PreviousState"] = args.PreviousState.ToString(),
            ["CurrentState"] = args.CurrentState.ToString()
        };

        await eventSink.PublishAsync(new EquipmentEvent(
            DeviceEventCodes.StateChanged,
            $"Device {device.DeviceId} state changed: {args.PreviousState} -> {args.CurrentState}.",
            EquipmentEventKind.StateChange,
            args.CurrentState == ConnectionState.Error
                ? EquipmentEventSeverity.Error
                : EquipmentEventSeverity.Information,
            device.DeviceId,
            Properties: properties)).ConfigureAwait(false);

        string connectionAlarmCode = GetConnectionAlarmCode(device.DeviceId);
        if (args.CurrentState == ConnectionState.Error)
        {
            await alarmService.RaiseAsync(new EquipmentAlarm(
                connectionAlarmCode,
                $"Device {device.DeviceId} entered Error state.",
                Source: device.DeviceId,
                Properties: properties)).ConfigureAwait(false);
        }
        else if (args.CurrentState == ConnectionState.Connected)
        {
            await alarmService.ClearAsync(
                connectionAlarmCode,
                $"Device {device.DeviceId} reconnected.").ConfigureAwait(false);
            await alarmService.ClearAsync(
                GetErrorAlarmCode(device.DeviceId),
                $"Device {device.DeviceId} returned to Connected.").ConfigureAwait(false);
        }
    }

    private async Task PublishDeviceErrorAsync(IDevice device, ErrorOccurredEventArgs args)
    {
        var properties = new Dictionary<string, string>
        {
            ["DeviceName"] = device.DeviceName,
            ["ExceptionType"] = args.Exception.GetType().FullName ?? args.Exception.GetType().Name,
            ["Exception"] = args.Exception.ToString()
        };

        await eventSink.PublishAsync(new EquipmentEvent(
            DeviceEventCodes.Error,
            args.Message,
            EquipmentEventKind.Alarm,
            EquipmentEventSeverity.Error,
            device.DeviceId,
            Properties: properties)).ConfigureAwait(false);

        await alarmService.RaiseAsync(new EquipmentAlarm(
            GetErrorAlarmCode(device.DeviceId),
            args.Message,
            Source: device.DeviceId,
            Properties: properties)).ConfigureAwait(false);
    }

    private Task PublishDeviceOperationAsync(IDevice device, DeviceOperationEventArgs args)
    {
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
            foreach ((string key, string value) in args.Properties)
            {
                properties[key] = value;
            }
        }

        if (args.Exception != null)
        {
            properties["ExceptionType"] = args.Exception.GetType().FullName ?? args.Exception.GetType().Name;
            properties["Exception"] = args.Exception.ToString();
        }

        return eventSink.PublishAsync(new EquipmentEvent(
            DeviceEventCodes.Operation,
            args.Message,
            EquipmentEventKind.Information,
            args.IsSuccess ? EquipmentEventSeverity.Information : EquipmentEventSeverity.Error,
            device.DeviceId,
            Properties: properties));
    }

    private void DetachCore(string deviceId)
    {
        if (!subscriptions.Remove(deviceId, out DeviceSubscription? subscription))
        {
            return;
        }

        Unsubscribe(subscription);
    }

    private static void Unsubscribe(DeviceSubscription subscription)
    {
        subscription.Device.StateChanged -= subscription.StateChanged;
        subscription.Device.ErrorOccurred -= subscription.ErrorOccurred;
        subscription.Device.OperationOccurred -= subscription.OperationOccurred;
    }

    private void Observe(Task task)
    {
        _ = task.ContinueWith(
            completed => logger.LogError(
                completed.Exception?.GetBaseException(),
                "Failed to forward a device event."),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static string GetConnectionAlarmCode(string deviceId)
        => $"DEVICE.{deviceId}.CONNECTION";

    private static string GetErrorAlarmCode(string deviceId)
        => $"DEVICE.{deviceId}.ERROR";

    private sealed record DeviceSubscription(
        IDevice Device,
        EventHandler<ConnectionStateChangedEventArgs> StateChanged,
        EventHandler<ErrorOccurredEventArgs> ErrorOccurred,
        EventHandler<DeviceOperationEventArgs> OperationOccurred);
}
