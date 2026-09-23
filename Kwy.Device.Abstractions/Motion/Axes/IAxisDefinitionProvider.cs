namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 设备级业务轴定义的只读查询入口。
/// 与 <c>IIoPointDefinitionProvider</c>、<c>IPlcPointDefinitionProvider</c> 对应：通过稳定 <c>axisId</c> 查询定义，
/// 不要求业务层知道运动卡型号、设备 ID 或物理轴通道。
/// </summary>
public interface IAxisDefinitionProvider
{
    /// <summary>设备当前加载的全部业务轴定义。</summary>
    IReadOnlyCollection<AxisDefinition> Definitions { get; }

    /// <summary>按稳定业务轴 ID 获取定义；不存在时抛出异常。</summary>
    AxisDefinition GetRequired(string axisId);

    /// <summary>尝试按稳定业务轴 ID 获取定义。</summary>
    bool TryGet(string axisId, out AxisDefinition definition);

    /// <summary>尝试按物理轴地址获取定义，供启动校验、状态映射和诊断使用。</summary>
    bool TryGet(AxisAddress address, out AxisDefinition definition);
}
