namespace KwyPecvd.Device.Mfc.Simulated;

public sealed class SimulatedMfc : MfcBase
{
    private double feedback;

    public SimulatedMfc(MfcDefinition definition)
        : base(definition, initiallyOffline: false)
    {
    }

    protected override ValueTask<MfcCycleResult> ExchangeAsync(
        double setPoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        feedback += (setPoint - feedback) * 0.20;
        return ValueTask.FromResult(
            new MfcCycleResult(feedback, IsOffline: false, DateTimeOffset.UtcNow));
    }
}
