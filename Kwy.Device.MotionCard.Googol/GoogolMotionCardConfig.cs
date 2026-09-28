using Kwy.Device.Abstractions;
using Kwy.Device.MotionCard.Abstractions;

namespace Kwy.Device.MotionCard.Googol;

/// <summary>
/// 固高运动控制卡的厂商连接与硬件能力配置。
/// </summary>
/// <remarks>
/// 本模型不替代厂商 <c>gts.cfg</c>。该文件保存控制器级硬件、电气与伺服参数，
/// 并在连接时由 <c>GT_LoadConfig</c> 加载。
/// 业务轴定义由设备级 <c>IAxisDefinitionProvider</c> 提供；本配置只保留固高控制器专属设置，
/// 不会把这些 C# 配置写回 <c>gts.cfg</c>。
/// </remarks>
public sealed class GoogolMotionCardConfig : IDeviceConfig
{
    public const int MaxSupportedAxisCount = 8;
    // GT_GetDi/GT_GetDo 只提供一个 32 位公共 IO 镜像。超过 31 的通道需要其他厂商 API，
    // 不能通过位移回绕伪造为当前卡的 IO。
    public const int MaxSupportedIoChannelCount = 32;
    public const short MaxSupportedCoordinateSystemCount = 2;

    public short CardNo { get; set; }

    /// <summary>设备注册的稳定标识；未设置时使用 <c>Googol-{CardNo}</c>。</summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// 传入 <c>GT_Open</c> 的 SDK 通信通道；GTS 脉冲控制器通常使用通道 0。
    /// </summary>
    public short OpenChannel { get; set; } = 0;

    /// <summary>
    /// 传入 <c>GT_Open</c> 的厂商打开参数，默认值为 1。
    /// </summary>
    public short OpenParameter { get; set; } = 1;

    public string Model { get; set; } = "GTS-800";

    /// <summary>
    /// 由 <c>GT_LoadConfig</c> 加载的厂商控制器配置，包含硬件映射、电平极性、滤波、
    /// 控制环等 GTS 控制器参数。
    /// </summary>
    /// <remarks>
    /// 文件由固高 SDK 解析和应用。本框架只把路径传给 <c>GT_LoadConfig</c>，
    /// 不会将 C# 中的业务轴定义或坐标系配置合并写入该文件。
    /// </remarks>
    public string? ConfigFilePath { get; set; } = "gts.cfg";

    public bool ResetOnConnect { get; set; } = true;

    public bool LoadConfigOnConnect { get; set; } = true;

    public short AxisCount { get; set; } = 8;

    /// <summary>一次 SDK 调用及同一临界区内连续读取的最大物理轴数。</summary>
    public short SnapshotBatchSize { get; set; } = 4;

    /// <summary>
    /// 多轴状态快照耗时达到此毫秒数时输出跟踪警告；设为 0 表示关闭警告。
    /// </summary>
    public double SnapshotSlowThresholdMilliseconds { get; set; } = 25;

    public short DiChannelCount { get; set; } = 16;

    public short DoChannelCount { get; set; } = 16;

    /// <summary>
    /// 固高控制器插补使用的坐标系配置。
    /// </summary>
    /// <remarks>
    /// 每个配置描述一个控制器坐标系使用的有序物理轴通道及其合成运动上限。
    /// 轴通道是控制器概念，不是业务轴 ID；业务轴定义仍由设备级目录统一维护。
    /// 这些配置不会写入 <c>gts.cfg</c>。
    /// </remarks>
    public IList<GoogolCoordinateSystemConfig> CoordinateSystems { get; } = new List<GoogolCoordinateSystemConfig>();

    /// <summary>
    /// GTS 公共 IO 通常低电平有效：逻辑有效 <c>true</c> 对应物理位为 0。
    /// </summary>
    public bool DigitalIoActiveLow { get; set; } = true;

    public bool Validate()
    {
        string resolvedDeviceId = DeviceId ?? $"Googol-{CardNo}";
        return CardNo >= 0
            && !string.IsNullOrWhiteSpace(resolvedDeviceId)
            && !string.IsNullOrWhiteSpace(Model)
            && (!LoadConfigOnConnect || !string.IsNullOrWhiteSpace(ConfigFilePath))
            && AxisCount is >= 1 and <= MaxSupportedAxisCount
            && SnapshotBatchSize is >= 1 and <= MaxSupportedAxisCount
            && double.IsFinite(SnapshotSlowThresholdMilliseconds)
            && SnapshotSlowThresholdMilliseconds >= 0
            && DiChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && DoChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public GoogolCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

}
