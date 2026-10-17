namespace KwyPecvd.Device.RfPowerSupply.Mks;

/// <summary>MKS 串口 RF 电源。</summary>
public sealed class MksRfPowerSupply : RfPowerSupplyBase
{
    private readonly MksRfPowerSupplyDefinition mksDefinition;
    private readonly IMksRfPowerBus bus;

    public MksRfPowerSupply(
        MksRfPowerSupplyDefinition definition,
        IMksRfPowerBus bus)
        : base(definition?.PowerSupply ?? throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();
        mksDefinition = definition;
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        mksDefinition.Validate();
        await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await WriteEnabledAsync(false, cancellationToken).ConfigureAwait(false);
        await WritePowerSetPointAsync(0, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask WritePowerSetPointAsync(
        double power,
        CancellationToken cancellationToken) =>
        new(bus.WritePowerAsync(power, cancellationToken));

    protected override ValueTask WriteEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken) =>
        new(bus.WriteEnabledAsync(enabled, cancellationToken));

    protected override async ValueTask<RfPowerSupplyCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        MksRfPowerStatus status = await bus.ReadStatusAsync(cancellationToken).ConfigureAwait(false);

        return new RfPowerSupplyCycleResult(
            ForwardPower: status.ForwardPower,
            ReflectedPower: status.ReflectedPower,
            IsOutputOn: status.ForwardPower > mksDefinition.OutputOnThreshold,
            IsOffline: !bus.IsConnected,
            IsFeedbackValid: true,
            IsFaulted: false,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
