using Kwy.Device.Abstractions.Equipment;
using KwyTemplate.Device.Options;

namespace KwyTemplate.Device.Connections;

public sealed class DeviceStartupService : IDeviceStartupService
{
    private const string StartupFailureCode = "Device.StartupConnectionFailed";
    private readonly IDeviceConnectionOptionsStore optionsStore;
    private readonly IDeviceConnectionService connectionService;
    private readonly IEquipmentProcessController processController;
    private readonly IEquipmentStateMachine stateMachine;
    private readonly IEquipmentEventSink eventSink;
    private readonly IAlarmService alarmService;

    public DeviceStartupService(
        IDeviceConnectionOptionsStore optionsStore,
        IDeviceConnectionService connectionService,
        IEquipmentProcessController processController,
        IEquipmentStateMachine stateMachine,
        IEquipmentEventSink eventSink,
        IAlarmService alarmService)
    {
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        this.processController = processController ?? throw new ArgumentNullException(nameof(processController));
        this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        this.alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        DeviceConnectionOptions options = await optionsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        DeviceConnectionEntry[] startupEntries = options.Devices
            .Where(static entry => entry.Enabled && entry.ConnectOnStartup)
            .ToArray();

        if (startupEntries.Length == 0)
        {
            await eventSink.PublishAsync(
                new EquipmentEvent(
                    "Device.Startup.NoStartupDevices",
                    "No device is configured to connect on startup.",
                    EquipmentEventSeverity.Information,
                    EquipmentEventKind.Event,
                    Source: null),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await stateMachine.ForceTransitionAsync(
            EquipmentRunState.Initializing,
            "Device startup connection started.",
            cancellationToken).ConfigureAwait(false);

        try
        {
            await connectionService.ConnectStartupDevicesAsync(cancellationToken).ConfigureAwait(false);

            EquipmentOperationResult initializeResult = await processController.InitializeAsync(cancellationToken).ConfigureAwait(false);
            if (!initializeResult.IsSuccess)
            {
                await RaiseStartupFailureAsync(
                    StartupFailureCode,
                    initializeResult.Message ?? "Equipment initialization failed.",
                    startupEntries,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RaiseStartupFailureAsync(
                StartupFailureCode,
                $"Device startup connection failed: {ex.Message}",
                startupEntries,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RaiseStartupFailureAsync(
        string failureCode,
        string message,
        IReadOnlyCollection<DeviceConnectionEntry> startupEntries,
        CancellationToken cancellationToken)
    {
        await stateMachine.ForceTransitionAsync(
            EquipmentRunState.ManualInterventionRequired,
            message,
            cancellationToken).ConfigureAwait(false);

        var properties = new Dictionary<string, string>
        {
            ["StartupDeviceIds"] = string.Join(",", startupEntries.Select(static entry => entry.DeviceId)),
            ["StartupDeviceTypes"] = string.Join(",", startupEntries.Select(static entry => entry.DeviceType)),
            ["ConnectOnStartup"] = true.ToString()
        };

        await eventSink.PublishAsync(
            new EquipmentEvent(
                failureCode,
                message,
                EquipmentEventSeverity.Warning,
                EquipmentEventKind.Alarm,
                Source: null,
                Properties: properties),
            cancellationToken).ConfigureAwait(false);

        await alarmService.RaiseAsync(
            new EquipmentAlarm(
                failureCode,
                message,
                EquipmentEventSeverity.Warning,
                Source: null,
                Properties: properties),
            cancellationToken).ConfigureAwait(false);
    }
}
