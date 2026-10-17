namespace KwyPecvd.Device.Thermal.TemperatureController.Omron;

/// <summary>Omron EJ1 温控通道。</summary>
public sealed class OmronEj1TemperatureController : TemperatureControllerBase
{
    private const ushort RunCommandBase = 0x0A00;
    private const ushort StopCommandBase = 0x0B00;

    private readonly OmronEj1Definition omronDefinition;
    private readonly IOmronEj1Bus bus;

    public OmronEj1TemperatureController(
        OmronEj1Definition definition,
        IOmronEj1Bus bus)
        : base(definition?.Controller ?? throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();
        omronDefinition = definition;
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        omronDefinition.Validate();
        await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await WriteEnabledAsync(false, cancellationToken).ConfigureAwait(false);
        await WriteTargetTemperatureAsync(
                Definition.MinimumSetPoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected override ValueTask WriteTargetTemperatureAsync(
        double target,
        CancellationToken cancellationToken)
    {
        short raw = checked((short)Math.Round(
            target * omronDefinition.RawUnitsPerDegree,
            MidpointRounding.AwayFromZero));

        return new ValueTask(bus.WriteSingleRegisterAsync(
            omronDefinition.UnitAddress,
            omronDefinition.SetPointRegister,
            unchecked((ushort)raw),
            cancellationToken));
    }

    protected override ValueTask WriteEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        ushort command = (ushort)((enabled ? RunCommandBase : StopCommandBase) +
                                  omronDefinition.Channel);

        return new ValueTask(bus.WriteSingleRegisterAsync(
            omronDefinition.UnitAddress,
            omronDefinition.RunStopRegister,
            command,
            cancellationToken));
    }

    protected override async ValueTask<TemperatureControllerCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        short raw = await bus.ReadHoldingRegisterAsync(
                omronDefinition.UnitAddress,
                omronDefinition.FeedbackRegister,
                cancellationToken)
            .ConfigureAwait(false);

        var isPowerOn = false;
        if (omronDefinition.PowerStatusRegister is ushort statusRegister)
        {
            short rawStatus = await bus.ReadHoldingRegisterAsync(
                    omronDefinition.UnitAddress,
                    statusRegister,
                    cancellationToken)
                .ConfigureAwait(false);

            isPowerOn =
                (unchecked((ushort)rawStatus) & omronDefinition.PowerOnMask) != 0;
        }

        return new TemperatureControllerCycleResult(
            Feedback: raw / omronDefinition.RawUnitsPerDegree,
            IsPowerOn: isPowerOn,
            IsOffline: !bus.IsConnected,
            IsFeedbackValid: true,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
