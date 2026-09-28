namespace Kwy.Device.MotionCard.Googol;

/// <summary>
/// 固高控制器的插补坐标系配置。
/// </summary>
/// <remarks>
/// 该模型把有序物理轴通道组成一个控制器插补坐标系，并定义合成速度、加速度和圆滑时间。
/// 它由固高适配器在运行时使用，不会从 <c>gts.cfg</c> 读取或写入 <c>gts.cfg</c>。
/// 业务层不应直接使用其中的物理轴通道。
/// </remarks>
public sealed class GoogolCoordinateSystemConfig
{
    /// <summary>固高控制器坐标系号，从 1 开始。</summary>
    public short CoordinateSystem { get; set; }

    /// <summary>构成该坐标系的有序物理轴通道；顺序必须与厂商插补 API 的轴顺序一致。</summary>
    public short[] Axes { get; set; } = Array.Empty<short>();

    /// <summary>该坐标系的最大合成速度，单位由参与轴共享的工程单位约定。</summary>
    public double MaximumVelocity { get; set; } = 500;

    /// <summary>该坐标系的最大合成加速度，单位由参与轴共享的工程单位约定。</summary>
    public double MaximumAcceleration { get; set; } = 2;

    /// <summary>传给控制器的插补圆滑时间；它是厂商控制器参数，不是上位机实时前瞻周期。</summary>
    public short SmoothingTime { get; set; } = 50;

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length
            && double.IsFinite(MaximumVelocity) && MaximumVelocity > 0
            && double.IsFinite(MaximumAcceleration) && MaximumAcceleration > 0
            && SmoothingTime >= 0;
}
