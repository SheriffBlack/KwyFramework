namespace KwyPecvd.Device.RfPowerSupply.Mks;

/// <summary>MKS RF 电源静态定义。</summary>
public sealed record MksRfPowerSupplyDefinition
{
    public required RfPowerSupplyDefinition PowerSupply { get; init; }

    /// <summary>正向功率超过该值时，视为硬件实际已有 RF 输出。</summary>
    public double OutputOnThreshold { get; init; } = 1;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(PowerSupply);
        PowerSupply.Validate();

        if (!double.IsFinite(OutputOnThreshold) || OutputOnThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(OutputOnThreshold));
    }
}
