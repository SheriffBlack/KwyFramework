namespace KwyPecvd.Device.Mfc.Fujikin;

/// <summary>Fujikin 数字通信 MFC 的静态定义。</summary>
public sealed record FujikinMfcDefinition
{
    public required MfcDefinition Mfc { get; init; }

    /// <summary>总线设备地址。</summary>
    public required byte Address { get; init; }

    /// <summary>
    /// MFC 满量程对应的原始协议值。
    /// 该值取决于具体型号，必须依据设备手册配置。
    /// </summary>
    public required short RawFullScale { get; init; }

    /// <summary>协议中一个 Ramp Time 原始单位对应的实际时间。</summary>
    public required TimeSpan RampTimeUnit { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Mfc);
        Mfc.Validate();

        if (RawFullScale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RawFullScale),
                RawFullScale,
                "Fujikin MFC raw full scale must be greater than zero.");
        }

        if (RampTimeUnit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RampTimeUnit),
                RampTimeUnit,
                "Fujikin MFC ramp-time unit must be greater than zero.");
        }
    }
}
