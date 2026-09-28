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
/// 设备具备读取并应用运行配置的能力。
/// 配置对象可由受控的配置服务编辑，但设备实例不允许在运行期被外部替换整份配置。
/// </summary>
public interface IConfigurableDevice
{
    IDeviceConfig DeviceParameter { get; }
    Task ApplyConfigAsync(CancellationToken cancellationToken = default);
}

