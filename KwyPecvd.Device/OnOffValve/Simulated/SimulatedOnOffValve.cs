namespace KwyPecvd.Device.OnOffValve.Simulated;

public sealed class SimulatedOnOffValve : OnOffValveBase
{
    private readonly object simulationSync = new();

    private readonly TimeSpan travelTime;

    private OnOffValvePosition actualPosition;

    private OnOffValvePosition motionTarget;

    private DateTimeOffset? movementStartedAt;

    private bool isOffline;

    private bool isFeedbackConflict;

    private bool isStuck;

    public SimulatedOnOffValve(
        OnOffValveDefinition definition,
        TimeSpan? travelTime = null)
        : base(definition)
    {
        this.travelTime =
            travelTime ?? TimeSpan.FromSeconds(1);

        if (this.travelTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(travelTime));
        }

        actualPosition = definition.SafePosition;
        motionTarget = definition.SafePosition;
    }

    protected override ValueTask WritePositionAsync(
        OnOffValvePosition position,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            if (isOffline)
            {
                throw new IOException(
                    $"Simulated valve '{Id}' is offline.");
            }

            if (motionTarget != position)
            {
                motionTarget = position;
                movementStartedAt = DateTimeOffset.UtcNow;
            }
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask<OnOffValveCycleResult> ReadFeedbackAsync(
        OnOffValvePosition commandedPosition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            if (isOffline)
            {
                return ValueTask.FromResult(
                    new OnOffValveCycleResult(
                        ActualPosition: null,
                        IsOffline: true,
                        IsFeedbackValid: false,
                        IsFeedbackConflict: false,
                        Timestamp: now));
            }

            if (isFeedbackConflict)
            {
                return ValueTask.FromResult(
                    new OnOffValveCycleResult(
                        ActualPosition: null,
                        IsOffline: false,
                        IsFeedbackValid: false,
                        IsFeedbackConflict: true,
                        Timestamp: now));
            }

            if (actualPosition != motionTarget &&
                movementStartedAt is null)
            {
                movementStartedAt = now;
            }

            if (!isStuck &&
                movementStartedAt is not null &&
                now - movementStartedAt.Value >=
                travelTime)
            {
                actualPosition = motionTarget;
                movementStartedAt = null;
            }

            OnOffValvePosition? feedbackPosition =
                movementStartedAt is null
                    ? actualPosition
                    : null;

            return ValueTask.FromResult(
                new OnOffValveCycleResult(
                    ActualPosition: feedbackPosition,
                    IsOffline: false,
                    IsFeedbackValid: true,
                    IsFeedbackConflict: false,
                    Timestamp: now));
        }
    }

    public void SetOffline(bool value)
    {
        lock (simulationSync)
        {
            isOffline = value;
        }
    }

    public void SetFeedbackConflict(bool value)
    {
        lock (simulationSync)
        {
            isFeedbackConflict = value;
        }
    }


    public void SetStuck(bool value)
    {
        lock (simulationSync)
        {
            isStuck = value;
        }
    }
}
