using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.MotionCards.Leadshine;

/// <summary>
/// 雷赛 DMC3800 控制卡的厂商连接与硬件能力配置。
/// 业务轴的单位、限位和回零策略不属于此模型，由设备级 <c>IAxisDefinitionProvider</c> 统一提供。
/// </summary>
public sealed class LeadshineMotionCardConfig : IDeviceConfig
{
    public const short SupportedAxisCount = 8;

    /// <summary>
    /// 框架对外支持的最大逻辑 IO 宽度。LTDMC 的两个 32 位端口组合为一个 64 位快照。
    /// </summary>
    public const int MaxSupportedIoChannelCount = 64;
    public const short MaxSupportedCoordinateSystemCount = 2;

    public short CardNo { get; set; }

    /// <summary>设备注册使用的稳定标识；未设置时使用 <c>Leadshine-{CardNo}</c>。</summary>
    public string? DeviceId { get; set; }

    public string Model => "DMC3800";

    /// <summary>
    /// 由 <c>dmc_download_configfile</c> 加载的雷赛厂商控制器配置文件。
    /// 文件用于控制器级硬件、电气和伺服参数；框架不会将业务轴定义写回该文件。
    /// </summary>
    public string? ConfigFilePath { get; set; } = "dmc.cfg";

    public bool ResetOnConnect { get; set; } = true;

    public bool LoadConfigOnConnect { get; set; } = true;

    public short AxisCount => SupportedAxisCount;

    public short DiChannelCount { get; set; } = 16;

    public short DoChannelCount { get; set; } = 16;

    /// <summary>按物理轴通道保存雷赛控制器专属参数，例如 LTDMC 回零模式。</summary>
    public IDictionary<short, LeadshineAxisOptions> AxisOptions { get; }
        = new Dictionary<short, LeadshineAxisOptions>();

    /// <summary>
    /// 雷赛控制器插补坐标系配置。
    /// 每个坐标系只描述有序物理轴通道；通道顺序必须与 LTDMC 插补 API 一致，
    /// 不能在业务层将其当作业务轴 ID 使用。
    /// </summary>
    public IList<LeadshineCoordinateSystemConfig> CoordinateSystems { get; } = new List<LeadshineCoordinateSystemConfig>();

    /// <summary>
    /// 雷赛公共 IO 的有效电平配置。设为 <c>true</c> 时，逻辑有效对应物理低电平。
    /// </summary>
    public bool DigitalIoActiveLow { get; set; } = true;

    public bool Validate()
    {
        string resolvedDeviceId = DeviceId ?? $"Leadshine-{CardNo}";
        return CardNo >= 0
            && !string.IsNullOrWhiteSpace(resolvedDeviceId)
            && (!LoadConfigOnConnect || !string.IsNullOrWhiteSpace(ConfigFilePath))
            && DiChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && DoChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && AxisOptions.Keys.All(axis => axis >= 1 && axis <= AxisCount)
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public LeadshineAxisOptions GetAxisOptions(short axis)
    {
        return AxisOptions.TryGetValue(axis, out LeadshineAxisOptions? options)
            ? options
            : new LeadshineAxisOptions();
    }

    public LeadshineCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

}
