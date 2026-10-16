using KwyPecvd.Device.Mfc.Ramp;

namespace KwyPecvd.Device.Mfc.Simulated;

public sealed class SimulatedMfc : MfcBase
{
    private double feedback;
    private double commandedSetPoint;

    public SimulatedMfc(MfcDefinition definition)
        : base(definition, new SoftwareRampProfile())
    {
    }

    protected override ValueTask WriteSetPointAsync(
        double setPoint,
        TimeSpan rampDuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        commandedSetPoint = setPoint;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<MfcCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        feedback += (commandedSetPoint - feedback) * 0.20;
        return ValueTask.FromResult(
            new MfcCycleResult(
                Feedback: feedback,
                IsOffline: false,
                IsFeedbackValid: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}
