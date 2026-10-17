namespace KwyPecvd.Device.RfMatcher.Mks;

/// <summary>MKS 双网络 RF 匹配器。</summary>
public sealed class MksRfMatcher : RfMatcherBase
{
    private readonly MksRfMatcherDefinition mksDefinition;

    private readonly IMksRfMatcherBus bus;

    public MksRfMatcher(
        MksRfMatcherDefinition definition,
        IMksRfMatcherBus bus)
        : base(
            definition?.Matcher ??
            throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();

        mksDefinition = definition;
        this.bus = bus ??
            throw new ArgumentNullException(nameof(bus));
    }

    protected override async Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        mksDefinition.Validate();

        await bus
            .ConnectAsync(cancellationToken)
            .ConfigureAwait(false);

        await StopMotionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected override ValueTask WriteModeAsync(
        RfMatcherMode mode,
        CancellationToken cancellationToken) =>
        new(
            bus.WriteModeAsync(
                mksDefinition.Address,
                mksDefinition.Network,
                mode,
                cancellationToken));

    protected override ValueTask WriteManualPositionsAsync(
        double tunePosition,
        double loadPosition,
        CancellationToken cancellationToken) =>
        new(
            bus.WriteManualPositionsAsync(
                mksDefinition.Address,
                mksDefinition.Network,
                tunePosition,
                loadPosition,
                cancellationToken));

    protected override ValueTask StopMotionAsync(
        CancellationToken cancellationToken) =>
        new(
            bus.StopMotionAsync(
                mksDefinition.Address,
                mksDefinition.Network,
                cancellationToken));

    protected override async ValueTask<RfMatcherCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        var status = await bus
            .ReadStatusAsync(
                mksDefinition.Address,
                mksDefinition.Network,
                cancellationToken)
            .ConfigureAwait(false);

        return new RfMatcherCycleResult(
            ActualMode: status.Mode,
            TunePosition: status.TunePosition,
            LoadPosition: status.LoadPosition,
            IsTuning:
                status.IsOutputOn &&
                status.Mode == RfMatcherMode.Auto &&
                !status.IsMatched,
            IsMatched: status.IsMatched,
            IsOffline: !bus.IsConnected,
            IsFeedbackValid: true,
            IsFaulted: status.IsFaulted,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
