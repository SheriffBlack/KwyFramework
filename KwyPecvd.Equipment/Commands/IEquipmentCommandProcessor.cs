namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 设备业务命令的统一处理入口。
/// </summary>
public interface IEquipmentCommandProcessor
{
    Task<EquipmentCommandResult> ExecuteAsync(EquipmentCommandEnvelope envelope, CancellationToken cancellationToken = default);
}
