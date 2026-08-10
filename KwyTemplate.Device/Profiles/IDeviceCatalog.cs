using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Profiles;

public interface IDeviceCatalog
{
    /// <summary>
    /// 设备清单标识，用于日志、诊断和后续按机型筛选。
    /// </summary>
    string CatalogKey { get; }

    /// <summary>
    /// 是否为模板默认清单。存在客户清单时，默认清单自动退让。
    /// </summary>
    bool IsDefault => false;

    /// <summary>
    /// 同一机型清单内的小设备选择配置标识，默认所有机型共用 DeviceSelection 这个配置槽位。
    /// </summary>
    string SelectionConfigId => "DeviceSelection";

    IReadOnlyList<DeviceDefinition> CreateDeviceDefinitions();
}
