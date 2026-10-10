namespace KwyPecvd.Process.Equipment;

/// <summary>
/// 机台内部一个可独立寻址的功能模块控制器。
/// </summary>
public interface IEquipmentModuleController
{
    string Id { get; }
}