namespace KwyPecvd.Device.ThrottleValve.Simulated;

/// <summary>
/// 节流阀仿真实现。
/// 写命令只改变仿真目标，周期读取才推进实际反馈。
/// </summary>
public sealed class SimulatedThrottleValve : ThrottleValveBase
{
    private readonly object simulationSync =  new();

    private readonly double positionResponseFactor;

    private readonly double pressureResponseFactor;

    private ThrottleValveMode requestedMode;

    private double requestedPosition;

    private double requestedPressure;

    private double actualPosition;

    private double actualPressure;

    private bool isOffline;

    private bool isPositionFeedbackValid = true;

    private bool isPressureFeedbackValid = true;

    private bool isFaulted;

    private int remainingWriteFailures;

    public SimulatedThrottleValve(
        ThrottleValveDefinition definition,
        double positionResponseFactor = 0.2,
        double pressureResponseFactor = 0.2)
        : base(definition)
    {
        ValidateResponseFactor(
            positionResponseFactor,
            nameof(positionResponseFactor));

        ValidateResponseFactor(
            pressureResponseFactor,
            nameof(pressureResponseFactor));

        this.positionResponseFactor =
            positionResponseFactor;

        this.pressureResponseFactor =
            pressureResponseFactor;

        requestedMode = definition.SafeMode;

        requestedPosition =
            definition.SafeMode ==
            ThrottleValveMode.Open
                ? definition.MaximumPosition
                : definition.MinimumPosition;

        actualPosition = requestedPosition;

        requestedPressure = 0;
        actualPressure = 0;
    }

    protected override ValueTask WriteCloseAsync(
    CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            ThrowIfWriteUnavailable();

            requestedMode =
                ThrottleValveMode.Closed;

            requestedPosition =
                Definition.MinimumPosition;
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask WriteOpenAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            ThrowIfWriteUnavailable();

            requestedMode =
                ThrottleValveMode.Open;

            requestedPosition =
                Definition.MaximumPosition;
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask WritePositionAsync(
        double position,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            ThrowIfWriteUnavailable();

            requestedMode =
                ThrottleValveMode.Position;

            requestedPosition = position;
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask WritePressureAsync(
        double pressure,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            ThrowIfWriteUnavailable();

            requestedMode =
                ThrottleValveMode.Pressure;

            requestedPressure = pressure;
        }

        return ValueTask.CompletedTask;
    }

    protected override
    ValueTask<ThrottleValveCycleResult>
    ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            if (!isOffline)
            {
                AdvanceSimulation();
            }

            return ValueTask.FromResult(
                new ThrottleValveCycleResult(
                    ActualMode:
                        isOffline
                            ? ThrottleValveMode.Unknown
                            : requestedMode,

                    PositionFeedback:
                        actualPosition,

                    PressureFeedback:
                        actualPressure,

                    IsOffline:
                        isOffline,

                    IsPositionFeedbackValid:
                        !isOffline &&
                        isPositionFeedbackValid,

                    IsPressureFeedbackValid:
                        !isOffline &&
                        isPressureFeedbackValid,

                    IsFaulted:
                        isFaulted,

                    Timestamp:
                        now));
        }
    }

    private void AdvanceSimulation()
    {
        switch (requestedMode)
        {
            case ThrottleValveMode.Closed:
                MoveTowards(
                    ref actualPosition,
                    Definition.MinimumPosition,
                    positionResponseFactor);
                break;

            case ThrottleValveMode.Open:
                MoveTowards(
                    ref actualPosition,
                    Definition.MaximumPosition,
                    positionResponseFactor);
                break;

            case ThrottleValveMode.Position:
                MoveTowards(
                    ref actualPosition,
                    requestedPosition,
                    positionResponseFactor);
                break;

            case ThrottleValveMode.Pressure:
                MoveTowards(
                    ref actualPressure,
                    requestedPressure,
                    pressureResponseFactor);
                break;
        }
    }

    private static void MoveTowards(
        ref double current,
        double target,
        double responseFactor)
    {
        current +=
            (target - current) *
            responseFactor;

        if (Math.Abs(target - current) <
            0.0001)
        {
            current = target;
        }
    }

    public void SetOffline(bool value)
    {
        lock (simulationSync)
        {
            isOffline = value;
        }
    }

    public void SetPositionFeedbackValid(
        bool value)
    {
        lock (simulationSync)
        {
            isPositionFeedbackValid = value;
        }
    }

    public void SetPressureFeedbackValid(
        bool value)
    {
        lock (simulationSync)
        {
            isPressureFeedbackValid = value;
        }
    }

    public void SetFaulted(bool value)
    {
        lock (simulationSync)
        {
            isFaulted = value;
        }
    }

    /// <summary>
    /// 让后续指定次数的硬件写入失败，
    /// 用于验证安全命令重试。
    /// </summary>
    public void FailNextWrites(int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count));
        }

        lock (simulationSync)
        {
            remainingWriteFailures = count;
        }
    }

    private void ThrowIfWriteUnavailable()
    {
        if (remainingWriteFailures > 0)
        {
            remainingWriteFailures--;

            throw new IOException(
                $"Simulated throttle valve " +
                $"'{Id}' write failed.");
        }

        if (isOffline)
        {
            throw new IOException(
                $"Simulated throttle valve " +
                $"'{Id}' is offline.");
        }
    }

    private static void ValidateResponseFactor(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) ||
            value <= 0 ||
            value > 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Response factor must be " +
                "greater than 0 and no more than 1.");
        }
    }
}