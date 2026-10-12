namespace KwyPecvd.Device.RfMatcher.Simulated;

public sealed class SimulatedRfMatcher : RfMatcherBase
{
    private readonly object simulationSync = new();

    private readonly double responseFactor;

    private double tunePosition;

    private double loadPosition;

    private double automaticTuneTarget;

    private double automaticLoadTarget;

    private bool isOffline;

    private bool hasBadQuality;

    private bool isFaulted;

    public SimulatedRfMatcher(
        RfMatcherDefinition definition,
        double responseFactor = 0.2,
        double automaticTuneTarget = 60,
        double automaticLoadTarget = 40)
        : base(definition)
    {
        if (!double.IsFinite(responseFactor) ||
            responseFactor <= 0 ||
            responseFactor > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseFactor));
        }

        ValidatePosition(
            automaticTuneTarget,
            nameof(automaticTuneTarget));

        ValidatePosition(
            automaticLoadTarget,
            nameof(automaticLoadTarget));

        this.responseFactor = responseFactor;
        this.automaticTuneTarget =
            automaticTuneTarget;
        this.automaticLoadTarget =
            automaticLoadTarget;

        tunePosition =
            definition.MinimumPosition;

        loadPosition =
            definition.MinimumPosition;
    }

    protected override ValueTask<
        RfMatcherCycleResult> ExchangeAsync(
            RfMatcherMode commandedMode,
            double tuneSetPoint,
            double loadSetPoint,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            var feedbackValid =
                !isOffline &&
                !hasBadQuality;

            if (!feedbackValid || isFaulted)
            {
                return ValueTask.FromResult(
                    new RfMatcherCycleResult(
                        ActualMode:
                            RfMatcherMode.Unknown,
                        TunePosition:
                            tunePosition,
                        LoadPosition:
                            loadPosition,
                        IsTuning: false,
                        IsMatched: false,
                        IsOffline:
                            isOffline,
                        IsFeedbackValid:
                            feedbackValid,
                        IsFaulted:
                            isFaulted,
                        Timestamp: now));
            }

            var actualMode = commandedMode;

            switch (actualMode)
            {
                case RfMatcherMode.Manual:
                    MoveTowards(
                        ref tunePosition,
                        tuneSetPoint);

                    MoveTowards(
                        ref loadPosition,
                        loadSetPoint);

                    break;

                case RfMatcherMode.Auto:
                    MoveTowards(
                        ref tunePosition,
                        automaticTuneTarget);

                    MoveTowards(
                        ref loadPosition,
                        automaticLoadTarget);

                    break;
            }

            var reachedAutomaticTarget =
                actualMode ==
                    RfMatcherMode.Auto &&
                IsNear(
                    tunePosition,
                    automaticTuneTarget) &&
                IsNear(
                    loadPosition,
                    automaticLoadTarget);

            return ValueTask.FromResult(
                new RfMatcherCycleResult(
                    ActualMode:
                        actualMode,
                    TunePosition:
                        tunePosition,
                    LoadPosition:
                        loadPosition,
                    IsTuning:
                        actualMode ==
                            RfMatcherMode.Auto &&
                        !reachedAutomaticTarget,
                    IsMatched:
                        reachedAutomaticTarget,
                    IsOffline: false,
                    IsFeedbackValid: true,
                    IsFaulted: false,
                    Timestamp: now));
        }
    }

    private void MoveTowards(
        ref double current,
        double target)
    {
        current +=
            (target - current) *
            responseFactor;

        if (Math.Abs(target - current) <
            0.001)
        {
            current = target;
        }
    }

    private bool IsNear(
        double current,
        double target)
    {
        return Math.Abs(current - target) <=
               Definition.PositionTolerance;
    }

    public void SetAutomaticMatchTarget(
        double tunePosition,
        double loadPosition)
    {
        ValidatePosition(
            tunePosition,
            nameof(tunePosition));

        ValidatePosition(
            loadPosition,
            nameof(loadPosition));

        lock (simulationSync)
        {
            automaticTuneTarget =
                tunePosition;

            automaticLoadTarget =
                loadPosition;
        }
    }

    public void SetOffline(bool value)
    {
        lock (simulationSync)
        {
            isOffline = value;
        }
    }

    public void SetBadQuality(bool value)
    {
        lock (simulationSync)
        {
            hasBadQuality = value;
        }
    }

    public void SetFaulted(bool value)
    {
        lock (simulationSync)
        {
            isFaulted = value;
        }
    }

    private void ValidatePosition(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) ||
            value <
                Definition.MinimumPosition ||
            value >
                Definition.MaximumPosition)
        {
            throw new ArgumentOutOfRangeException(
                parameterName);
        }
    }
}