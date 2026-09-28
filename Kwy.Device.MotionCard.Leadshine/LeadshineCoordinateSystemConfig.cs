namespace Kwy.Device.MotionCard.Leadshine;

/// <summary>
/// 雷赛控制器的插补坐标系配置。
/// 仅负责将有序物理轴通道绑定到控制器坐标系；业务轴 ID、工程单位和限位由设备级轴定义维护。
/// </summary>
public sealed class LeadshineCoordinateSystemConfig
{
    /// <summary>雷赛控制器坐标系号，从 1 开始。</summary>
    public short CoordinateSystem { get; set; }

    /// <summary>构成坐标系的有序物理轴通道；顺序必须与 LTDMC 插补 API 的轴顺序一致。</summary>
    public short[] Axes { get; set; } = Array.Empty<short>();

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length;
}
