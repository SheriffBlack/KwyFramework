namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public async Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(
        GemRemoteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.RemoteCommands.TryGetValue(command.CommandName, out GemRemoteCommandDefinition? definition))
            return new(GemHostCommandAckCode.CommandNotFound, $"Remote command {command.CommandName} is not defined.", GemRemoteCommandCompletionStatus.Rejected);

        GemRemoteCommandValidationResult validation = GemRemoteCommandValidator.Validate(definition, command, ControlState);
        if (!validation.IsValid)
            return new(validation.AckCode, validation.Message, GemRemoteCommandCompletionStatus.Rejected);

        if (!registry.TryGetCommand(definition.EffectiveHandlerKey, out GemRemoteCommandHandler handler))
            return new(
                GemHostCommandAckCode.CommandNotFound,
                $"Remote command handler {definition.EffectiveHandlerKey} is not registered.",
                GemRemoteCommandCompletionStatus.Rejected);

        try
        {
            GemRemoteCommandResult result = await handler(command, cancellationToken).ConfigureAwait(false);
            return result.CompletionStatus != GemRemoteCommandCompletionStatus.Unspecified
                ? result
                : result.WithInferredCompletionStatus();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(GemHostCommandAckCode.CannotPerformNow, "Remote command execution was cancelled.", GemRemoteCommandCompletionStatus.Cancelled);
        }
        catch (Exception exception)
        {
            return new(GemHostCommandAckCode.CannotPerformNow, exception.Message, GemRemoteCommandCompletionStatus.Failed);
        }
    }

    public async Task<bool> ReportRemoteCommandStatusAsync(
        string commandName,
        GemRemoteCommandCompletionStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        if (!registry.Catalog.RemoteCommands.TryGetValue(commandName, out GemRemoteCommandDefinition? definition))
            throw new KeyNotFoundException($"Remote command '{commandName}' is not defined.");

        GemCeid? eventId = status switch
        {
            GemRemoteCommandCompletionStatus.Accepted => definition.AcceptedEvent,
            GemRemoteCommandCompletionStatus.Completed => definition.CompletedEvent,
            GemRemoteCommandCompletionStatus.Failed or GemRemoteCommandCompletionStatus.Cancelled => definition.FailedEvent,
            GemRemoteCommandCompletionStatus.Unspecified or GemRemoteCommandCompletionStatus.Rejected => null,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
        if (eventId is null)
            return false;
        await ReportEventAsync(eventId.Value.Value, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
