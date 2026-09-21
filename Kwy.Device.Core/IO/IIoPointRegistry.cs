namespace Kwy.Device.Core.IO;

/// <summary>
/// 可选的诊断显示名称登记能力。
/// 不属于物理 IO 卡或业务逻辑点位契约；业务应使用 IoPoint.Id 与 IoPoint.Name。
/// </summary>
public interface IIoPointRegistry
{
    void SetDoName(int channel, string name);
    IEnumerable<(int Index, string Name)> GetAllOutputs();
    void SetDiName(int channel, string name);
    IEnumerable<(int Index, string Name)> GetAllInputs();
}
