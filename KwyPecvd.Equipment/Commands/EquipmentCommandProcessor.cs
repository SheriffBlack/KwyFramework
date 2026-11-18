using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Modules;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 设备业务命令的默认处理器。
/// </summary>
public sealed class EquipmentCommandProcessor : IEquipmentCommandProcessor
{
    private readonly IEquipmentModuleRegistry moduleRegistry;

    private readonly IPendingEquipmentCommandStore pendingCommandStore;

    private readonly IEquipmentCommandResultStore commandResultStore;

    public EquipmentCommandProcessor(
        IEquipmentModuleRegistry moduleRegistry,
        IPendingEquipmentCommandStore pendingCommandStore, 
        IEquipmentCommandResultStore commandResultStore)
    {
        ArgumentNullException.ThrowIfNull(moduleRegistry);
        ArgumentNullException.ThrowIfNull(pendingCommandStore);
        ArgumentNullException.ThrowIfNull(commandResultStore);

        this.moduleRegistry = moduleRegistry;
        this.pendingCommandStore = pendingCommandStore;
        this.commandResultStore = commandResultStore;
    }

    public Task<EquipmentCommandResult> ExecuteAsync(EquipmentCommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Command switch
        {
            InitializeEquipmentCommand command =>
                InitializeEquipmentAsync(
                    envelope,
                    command,
                    cancellationToken),

            ResetEquipmentFaultCommand command =>
                ResetEquipmentFaultAsync(
                    envelope,
                    command,
                    cancellationToken),

            StopProcessCommand command =>
                StopProcessAsync(
                    envelope,
                    command,
                    cancellationToken),

            _ => Task.FromResult(
                new EquipmentCommandResult(
                    envelope.Context.RequestId,
                    EquipmentCommandStatus.Rejected,
                    EquipmentCommandResultCodes
                        .UnsupportedCommand,
                    $"Command type " +
                    $"'{envelope.Command.GetType().Name}' " +
                    "is not supported."))
        };
    }

    private async Task<EquipmentCommandResult> InitializeEquipmentAsync(
            EquipmentCommandEnvelope envelope,
            InitializeEquipmentCommand command,
            CancellationToken cancellationToken)
    {
        var failure = TryResolveChamber(
            envelope.Context.RequestId,
            command.EquipmentModuleId,
            out var chamber);

        if (failure is not null)
        {
            return failure;
        }

        var result = await chamber!.InitializeAsync(cancellationToken);

        return ToEquipmentCommandResult(
            envelope.Context.RequestId,
            result.Succeeded,
            result.Code,
            result.Message);
    }

    private async Task<EquipmentCommandResult> ResetEquipmentFaultAsync(
            EquipmentCommandEnvelope envelope,
            ResetEquipmentFaultCommand command,
            CancellationToken cancellationToken)
    {
        var failure = TryResolveChamber(
            envelope.Context.RequestId,
            command.EquipmentModuleId,
            out var chamber);

        if (failure is not null)
        {
            return failure;
        }

        var result = await chamber!.ResetFaultAsync(cancellationToken);

        return ToEquipmentCommandResult(
            envelope.Context.RequestId,
            result.Succeeded,
            result.Code,
            result.Message);
    }

    private async Task<EquipmentCommandResult> StopProcessAsync(
        EquipmentCommandEnvelope envelope,
        StopProcessCommand command,
        CancellationToken cancellationToken)
    {
        var failure = TryResolveChamber(
            envelope.Context.RequestId,
            command.EquipmentModuleId,
            out var chamber);

        if (failure is not null)
        {
            return failure;
        }
        var pending =
            new PendingEquipmentCommand(
                Context: envelope.Context,
                CommandTypeCode: EquipmentCommandTypeCodes.StopProcess,
                EquipmentModuleId: command.EquipmentModuleId,
                TrackedAt: DateTimeOffset.UtcNow);

        if (!pendingCommandStore.TryAdd(pending))
        {
            return new EquipmentCommandResult(
                envelope.Context.RequestId,
                EquipmentCommandStatus.Rejected,
                EquipmentCommandResultCodes.AlreadyPending,
                $"Equipment module " +
                $"'{command.EquipmentModuleId}' " +
                "already has a pending stop command.");
        }

        try
        {
            var accepted =
                new EquipmentCommandResult(
                    envelope.Context.RequestId,
                    EquipmentCommandStatus.Accepted,
                    EquipmentCommandResultCodes.Accepted,
                    $"Stop process command for equipment module " +
                    $"'{command.EquipmentModuleId}' is being submitted.");

            SetStopProcessResult(
                command.EquipmentModuleId,
                accepted);

            var result =
                await chamber!.StopProcessAsync(
                    cancellationToken);

            if (!result.Succeeded)
            {
                pendingCommandStore.TryRemove(
                    command.EquipmentModuleId,
                    EquipmentCommandTypeCodes.StopProcess,
                    envelope.Context.RequestId);

                var rejected = ToEquipmentCommandResult(
                    envelope.Context.RequestId,
                    result.Succeeded,
                    result.Code,
                    result.Message);

                SetStopProcessResult(
                    command.EquipmentModuleId,
                    rejected);

                return rejected;
            }

            var acceptedResult = accepted with
            {
                Message = result.Message
            };

            SetStopProcessResult(
                command.EquipmentModuleId,
                acceptedResult);

            return acceptedResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            pendingCommandStore.TryRemove(
                command.EquipmentModuleId,
                EquipmentCommandTypeCodes.StopProcess,
                envelope.Context.RequestId);

            SetStopProcessResult(
                command.EquipmentModuleId,
                new EquipmentCommandResult(
                    envelope.Context.RequestId,
                    EquipmentCommandStatus.Rejected,
                    EquipmentCommandResultCodes.Canceled,
                    "Stop process command was canceled " +
                    "before it was accepted by the process module."));

            throw;
        }
        catch (Exception exception)
        {
            pendingCommandStore.TryRemove(
                command.EquipmentModuleId,
                EquipmentCommandTypeCodes.StopProcess,
                envelope.Context.RequestId);

            SetStopProcessResult(
                command.EquipmentModuleId,
                new EquipmentCommandResult(
                    envelope.Context.RequestId,
                    EquipmentCommandStatus.Failed,
                    EquipmentCommandResultCodes.ExecutionFailed,
                    exception.Message));

            throw;
        }
    }

    private void SetStopProcessResult(
        string equipmentModuleId,
        EquipmentCommandResult result)
    {
        commandResultStore.Set(
            new EquipmentCommandResultSnapshot(
                RequestId: result.RequestId,
                CommandTypeCode: EquipmentCommandTypeCodes.StopProcess,
                EquipmentModuleId: equipmentModuleId,
                Status: result.Status,
                Code: result.Code,
                Message: result.Message,
                UpdatedAt: DateTimeOffset.UtcNow));
    }

    private EquipmentCommandResult? TryResolveChamber(
        string requestId,
        string equipmentModuleId,
        out IChamberController? chamber)
    {
        chamber = null;

        if (string.IsNullOrWhiteSpace(equipmentModuleId))
        {
            return new EquipmentCommandResult(
                requestId,
                EquipmentCommandStatus.Rejected,
                EquipmentCommandResultCodes.InvalidArgument,
                "Equipment module id is required.");
        }

        if (!moduleRegistry.TryGet<IChamberController>(equipmentModuleId, out chamber))
        {
            return new EquipmentCommandResult(
                requestId,
                EquipmentCommandStatus.Rejected,
                EquipmentCommandResultCodes.ModuleNotFound,
                $"Equipment module " +
                $"'{equipmentModuleId}' " +
                "was not found.");
        }

        return null;
    }

    private static EquipmentCommandResult ToEquipmentCommandResult(
        string requestId,
        bool succeeded,
        string code,
        string message)
    {
        return new EquipmentCommandResult(
            requestId,
            succeeded
                ? EquipmentCommandStatus.Completed
                : EquipmentCommandStatus.Rejected,
            code,
            message);
    }
}
