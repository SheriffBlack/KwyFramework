using KwyPecvd.Device.Mfc.Ramp;

namespace KwyPecvd.Device.Mfc.Fujikin;

/// <summary>通过 Fujikin 数字协议工作的质量流量控制器。</summary>
public sealed class FujikinMfc : MfcBase
{
    private readonly FujikinMfcDefinition definition;
    private readonly IFujikinMfcBus bus;

    public FujikinMfc(
        FujikinMfcDefinition definition,
        IFujikinMfcBus bus)
        : base(
            definition?.Mfc ??
            throw new ArgumentNullException(nameof(definition)),
            new NativeRampProfile())
    {
        definition.Validate();

        this.definition = definition;
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async ValueTask WriteSetPointAsync(
        double setPoint,
        TimeSpan rampDuration,
        CancellationToken cancellationToken)
    {
        int rawRampTime = checked(
            (int)Math.Round(
                rampDuration.TotalMilliseconds /
                definition.RampTimeUnit.TotalMilliseconds,
                MidpointRounding.AwayFromZero));
        short rawSetPoint = ToRawValue(setPoint);

        await bus
            .WriteRampTimeAsync(definition.Address, rawRampTime, cancellationToken)
            .ConfigureAwait(false);
        await bus
            .WriteSetPointAsync(definition.Address, rawSetPoint, cancellationToken)
            .ConfigureAwait(false);
    }

    protected override async ValueTask<MfcCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        short rawFeedback =
            await bus
                .ReadIndicatedFlowAsync(
                    definition.Address,
                    cancellationToken)
                .ConfigureAwait(false);

        double feedback = ToEngineeringValue(rawFeedback);

        return new MfcCycleResult(
            feedback,
            IsOffline: false,
            IsFeedbackValid:
                rawFeedback >= 0 &&
                rawFeedback <= definition.RawFullScale,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private short ToRawValue(double engineeringValue)
    {
        double ratio = engineeringValue / Definition.FullScale;
        return checked(
            (short)Math.Round(
                ratio * definition.RawFullScale,
                MidpointRounding.AwayFromZero));
    }

    private double ToEngineeringValue(short rawValue) =>
        rawValue * Definition.FullScale /
        definition.RawFullScale;
}
