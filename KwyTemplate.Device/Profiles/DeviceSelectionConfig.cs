using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// DCR 工位仪表选择方式。
/// </summary>
public enum DeviceSelectionMode
{
    /// <summary>
    /// 按配置中指定的型号创建设备。
    /// </summary>
    Manual,

    /// <summary>
    /// 连接前通过 GPIB *IDN? 自动识别仪表型号。
    /// </summary>
    AutoDetect
}

/// <summary>
/// DCR 工位可选仪表型号。
/// 同一工位的小设备差异放在 Selection 中，避免为每种组合复制一套 Catalog。
/// </summary>
public enum DcrMeterModel
{
    [Description("ADEX 1152D")]
    AdexDcr,

    [Description("HIOKI 3542")]
    HiokiLcr
}

/// <summary>
/// 默认设备清单下的小设备选择配置。
/// 适合处理“同一客户同一机型下，某个工位仪表品牌可选”的场景。
/// </summary>
public sealed class DeviceSelectionConfig
{
    [Category("DCR 工位 1")]
    [DisplayName("选择方式")]
    [InputType(InputType.ComboBox)]
    public DeviceSelectionMode DcrMeter1Mode { get; set; } = DeviceSelectionMode.Manual;

    [Category("DCR 工位 1")]
    [DisplayName("手动型号")]
    [InputType(InputType.ComboBox)]
    public DcrMeterModel DcrMeter1 { get; set; } = DcrMeterModel.AdexDcr;

    [Category("DCR 工位 1")]
    [DisplayName("自动识别失败时使用手动型号")]
    public bool DcrMeter1FallbackToManual { get; set; } = true;
}
