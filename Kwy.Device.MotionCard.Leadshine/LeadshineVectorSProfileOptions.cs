namespace Kwy.Device.MotionCard.Leadshine;

/// <summary>
/// LTDMC 向量插补 S 曲线的原生参数。
/// <para>Mode 与 Parameter 的确切语义由已确认的控制器型号、固件和 LTDMC 手册决定；
/// 它们不是跨厂商的 Jerk 单位，不能放入业务 <c>MotionProfile</c>。</para>
/// </summary>
public sealed class LeadshineVectorSProfileOptions
{
    /// <summary>LTDMC <c>s_mode</c> 原生取值。</summary>
    public ushort Mode { get; set; }

    /// <summary>LTDMC <c>s_para</c> 原生取值。</summary>
    public double Parameter { get; set; }

    public bool Validate() => double.IsFinite(Parameter) && Parameter >= 0;
}
