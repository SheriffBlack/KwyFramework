namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

/// <summary>Pfeiffer A100 串口真空泵。</summary>
public sealed class PfeifferA100VacuumPump : VacuumPumpBase
{
    private readonly PfeifferA100Definition pfeifferDefinition;
    private readonly IPfeifferA100Bus bus;

    public PfeifferA100VacuumPump(
        PfeifferA100Definition definition,
        IPfeifferA100Bus bus)
        : base(definition?.Pump ?? throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();
        pfeifferDefinition = definition;
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        pfeifferDefinition.Validate();
        await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await WriteRunningAsync(false, cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask WriteRunningAsync(
        bool running,
        CancellationToken cancellationToken) =>
        new(bus.WriteRunningAsync(
            pfeifferDefinition.Address,
            running,
            cancellationToken));

    protected override async ValueTask<VacuumPumpCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        PfeifferA100Status status = await bus.ReadStatusAsync(
                pfeifferDefinition.Address,
                cancellationToken)
            .ConfigureAwait(false);

        return new VacuumPumpCycleResult(
            IsRunning: status.IsRunning,
            HasWarning: status.HasWarning,
            HasAlarm: status.HasAlarm || status.IsOverTemperature,
            IsOverloaded: false,
            IsN2Ready: true,
            IsExhaustReady: true,
            IsOffline: !bus.IsConnected,
            IsFeedbackValid: true,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
