using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Equipment;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 设备业务命令的默认处理器。
/// </summary>
public sealed class EquipmentCommandProcessor
    : IEquipmentCommandProcessor
{
    private readonly IEquipmentModuleRegistry moduleRegistry;

    public EquipmentCommandProcessor(
        IEquipmentModuleRegistry moduleRegistry)
    {
        ArgumentNullException.ThrowIfNull(moduleRegistry);

        this.moduleRegistry = moduleRegistry;
    }

    public Task<EquipmentCommandResult> ExecuteAsync(
        EquipmentCommandEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Command switch
        {
            InitializeEquipmentCommand command =>
                InitializeEquipmentAsync(
                    envelope.RequestId,
                    command,
                    cancellationToken),

            _ => Task.FromResult(
                new EquipmentCommandResult(
                    envelope.RequestId,
                    EquipmentCommandStatus.Rejected,
                    "UNSUPPORTED_COMMAND",
                    $"Command type " +
                    $"'{envelope.Command.GetType().Name}' " +
                    "is not supported."))
        };
    }

    private async Task<EquipmentCommandResult>
        InitializeEquipmentAsync(
            string requestId,
            InitializeEquipmentCommand command,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                command.EquipmentModuleId))
        {
            return new EquipmentCommandResult(
                requestId,
                EquipmentCommandStatus.Rejected,
                "INVALID_ARGUMENT",
                "Equipment module id is required.");
        }

        if (!moduleRegistry.TryGet<IChamberController>(
                command.EquipmentModuleId,
                out var chamber))
        {
            return new EquipmentCommandResult(
                requestId,
                EquipmentCommandStatus.Rejected,
                "MODULE_NOT_FOUND",
                $"Equipment module " +
                $"'{command.EquipmentModuleId}' " +
                "was not found.");
        }

        var result = await chamber.InitializeAsync(
            cancellationToken);

        return new EquipmentCommandResult(
            requestId,
            result.Succeeded
                ? EquipmentCommandStatus.Completed
                : EquipmentCommandStatus.Rejected,
            result.Code,
            result.Message);
    }
}
