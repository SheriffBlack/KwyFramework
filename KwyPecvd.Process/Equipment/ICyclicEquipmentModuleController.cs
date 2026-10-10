namespace KwyPecvd.Process.Equipment;

/// <summary>需要由RT周期推进的设备模块。</summary>
public interface ICyclicEquipmentModuleController
    : IEquipmentModuleController
{
    Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default);
}
