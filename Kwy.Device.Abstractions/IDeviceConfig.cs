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

/// <summary>
/// 设备具备暴露和应用可变配置模型的能力。
/// </summary>
public interface IConfigurableDevice
{
    IDeviceConfig DeviceParameter { get; set; }
    Task ApplyConfigAsync(CancellationToken cancellationToken = default);
}

