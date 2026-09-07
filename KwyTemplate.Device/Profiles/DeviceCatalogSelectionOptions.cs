namespace KwyTemplate.Device.Profiles;

/// <summary>
/// 当前启用的设备清单选择项。
/// 注册多个 IDeviceCatalog 不代表全部启用；启动时只加载 ActiveCatalogKey 指定的清单。
/// </summary>
public sealed class DeviceCatalogSelectionOptions
{
    /// <summary>
    /// 当前启用的设备清单标识。默认使用模板基础清单。
    /// </summary>
    public string ActiveCatalogKey { get; set; } = "Default";
}
