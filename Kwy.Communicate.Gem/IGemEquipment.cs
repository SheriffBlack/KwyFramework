namespace Kwy.Communicate.Gem;

public interface IGemEquipment
{
    GemCommunicationState CommunicationState { get; }

    GemControlState ControlState { get; }

    GemCommunicationContext Context { get; }

    Task EstablishCommunicationAsync(CancellationToken cancellationToken = default);

    Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default);

    Task SetOfflineAsync(CancellationToken cancellationToken = default);

    Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default);

    Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default);

    Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default);

    Task SaveRecipeAsync(GemRecipe recipe, CancellationToken cancellationToken = default);

    Task<GemTraceSample> CaptureTraceAsync(uint traceId, uint sampleNumber, CancellationToken cancellationToken = default);

    Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(
        GemRemoteCommand command,
        CancellationToken cancellationToken = default);
}
