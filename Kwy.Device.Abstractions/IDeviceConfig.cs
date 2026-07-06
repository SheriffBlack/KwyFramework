namespace Kwy.Device.Abstractions;

/// <summary>
/// 设备参数配置接口
/// </summary>
public interface IDeviceConfig
{
    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    bool Validate();
}

// ═══════════════════════════════════════════════════════════════════════════
// 能力接口 (Capability Interfaces)
// 每个接口代表一种物理能力，仪器的 Config 类只需"拼装"它支持的接口
// UI 引擎通过 is 模式匹配来决定是否渲染对应控件
// 新增仪器时只需创建新 Config 类并实现对应接口，无需修改任何已有代码 → OCP
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// 能力：支持热电动势补偿 (OVC)  —— 如 GOM804
/// </summary>
public interface IHasOvcCompensation
{
    bool EnableOVC { get; set; }
}

/// <summary>
/// 能力：支持温度补偿 (T.CONV) —— 如 GOM804, IM3533
/// </summary>
public interface IHasTemperatureCompensation
{
    bool EnableTConv { get; set; }
    double BaseTemperature { get; set; }
}

/// <summary>
/// 能力：支持直流偏置 (DC Bias) —— 如 IM3536, E4982A
/// </summary>
public interface IHasDCBias
{
    bool EnableDCBias { get; set; }
    double DCBiasValue { get; set; }
}

/// <summary>
/// 能力：支持自动电平控制 (ALC) —— 如 IM3536 (MLCC 测试必开)
/// </summary>
public interface IHasAutoLevelControl
{
    bool EnableALC { get; set; }
}

/// <summary>
/// Device capability for exposing and applying a mutable configuration model.
/// </summary>
public interface IConfigurableDevice
{
    IDeviceConfig DeviceParameter { get; set; }
    Task ApplyConfigurationAsync(CancellationToken cancellationToken = default);
}
