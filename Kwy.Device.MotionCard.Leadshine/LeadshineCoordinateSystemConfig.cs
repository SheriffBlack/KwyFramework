using Kwy.Device.MotionCard.Abstractions;

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

    /// <summary>
    /// 雷赛控制器原生向量 S 曲线参数。该参数只在业务动作明确要求原生 S 曲线时应用；
    /// 未配置时，不能将该坐标系用于 <see cref="MotionSmoothingMode.RequireNativeSCurve"/> 动作。
    /// </summary>
    public LeadshineVectorSProfileOptions? SProfile { get; set; }

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length
            && (SProfile is null || SProfile.Validate());
}
