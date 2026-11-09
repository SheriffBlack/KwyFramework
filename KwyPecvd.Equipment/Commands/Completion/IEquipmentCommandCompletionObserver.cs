using KwyPecvd.Process.Modules;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 观察模块周期后的最终状态，完成此前已接受的异步命令。
/// </summary>
public interface IEquipmentCommandCompletionObserver
{
    Task ObserveAsync(
        IEquipmentModuleController module,
        CancellationToken cancellationToken = default);
}
