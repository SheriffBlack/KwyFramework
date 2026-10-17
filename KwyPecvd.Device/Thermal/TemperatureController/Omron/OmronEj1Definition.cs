namespace KwyPecvd.Device.Thermal.TemperatureController.Omron;

/// <summary>Omron EJ1 单个温控通道的静态定义。</summary>
public sealed record OmronEj1Definition
{
    public required TemperatureControllerDefinition Controller { get; init; }
    public required byte UnitAddress { get; init; }
    public required ushort FeedbackRegister { get; init; }
    public required ushort SetPointRegister { get; init; }
    /// <summary>可选的输出状态寄存器；未配置时不推测物理加热状态。</summary>
    public ushort? PowerStatusRegister { get; init; }
    public ushort PowerOnMask { get; init; } = 0x0001;
    public ushort RunStopRegister { get; init; } = 0xFFFF;
    public required byte Channel { get; init; }
    public double RawUnitsPerDegree { get; init; } = 10;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Controller);
        Controller.Validate();

        if (UnitAddress is 0 or > 247)
            throw new ArgumentOutOfRangeException(nameof(UnitAddress));
        if (Channel > 3)
            throw new ArgumentOutOfRangeException(nameof(Channel));
        if (!double.IsFinite(RawUnitsPerDegree) || RawUnitsPerDegree <= 0)
            throw new ArgumentOutOfRangeException(nameof(RawUnitsPerDegree));
        if (PowerStatusRegister is not null && PowerOnMask == 0)
            throw new ArgumentOutOfRangeException(nameof(PowerOnMask));
    }
}