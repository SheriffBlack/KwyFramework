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

    /// <summary>传入 <c>GT_SetCrdPrm</c> 的 <c>synVelMax</c> 原生合成速度上限。</summary>
    public double SynchronousVelocityLimit { get; set; } = 500;

    /// <summary>传入 <c>GT_SetCrdPrm</c> 的 <c>synAccMax</c> 原生合成加速度上限。</summary>
    public double SynchronousAccelerationLimit { get; set; } = 2;

    /// <summary>传入 <c>GT_SetCrdPrm</c> 的 <c>evenTime</c> 原生拐角平滑时间；不是上位机实时前瞻周期。</summary>
    public short CornerSmoothingTime { get; set; } = 50;

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length
            && double.IsFinite(SynchronousVelocityLimit) && SynchronousVelocityLimit > 0
            && double.IsFinite(SynchronousAccelerationLimit) && SynchronousAccelerationLimit > 0
            && CornerSmoothingTime >= 0;
}
