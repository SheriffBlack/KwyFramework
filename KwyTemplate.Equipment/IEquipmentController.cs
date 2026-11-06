namespace KwyTemplate.Equipment;

/// <summary>整机生产命令的唯一应用入口。</summary>
public interface IEquipmentController
{
    event EventHandler? StateChanged;

    EquipmentState State { get; }

    bool IsProductionRunning { get; }

    EquipmentCommandEvaluation Evaluate(EquipmentCommand command);

    Task<EquipmentCommandResult> StartProductionAsync(CancellationToken cancellationToken = default);

    Task<EquipmentCommandResult> StopProductionAsync(CancellationToken cancellationToken = default);
}
