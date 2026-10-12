namespace KwyPecvd.Device.VacuumPump.Simulated;

public sealed class SimulatedVacuumPump : VacuumPumpBase
{
    private readonly object simulationSync =
        new();

    private bool hasWarning;
    private bool hasAlarm;
    private bool isOverloaded;
    private bool isN2Ready = true;
    private bool isExhaustReady = true;
    private bool isOffline;

    public SimulatedVacuumPump(
        VacuumPumpDefinition definition)
        : base(definition)
    {
    }

    public void SetFaultState(
        bool warning,
        bool alarm,
        bool overloaded)
    {
        lock (simulationSync)
        {
            hasWarning = warning;
            hasAlarm = alarm;
            isOverloaded = overloaded;
        }
    }

    public void SetUtilityState(
        bool n2Ready,
        bool exhaustReady)
    {
        lock (simulationSync)
        {
            isN2Ready = n2Ready;
            isExhaustReady = exhaustReady;
        }
    }

    public void SetOffline(bool offline)
    {
        lock (simulationSync)
        {
            isOffline = offline;
        }
    }

    protected override ValueTask<
        VacuumPumpCycleResult>
        ExchangeAsync(
            bool commandedOn,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var feedbackValid = !isOffline;

            var running =
                commandedOn &&
                feedbackValid &&
                !hasAlarm &&
                !isOverloaded;

            return ValueTask.FromResult(
                new VacuumPumpCycleResult(
                    IsRunning: running,
                    HasWarning: hasWarning,
                    HasAlarm: hasAlarm,
                    IsOverloaded: isOverloaded,
                    IsN2Ready: isN2Ready,
                    IsExhaustReady:
                        isExhaustReady,
                    IsOffline: isOffline,
                    IsFeedbackValid:
                        feedbackValid,
                    Timestamp:
                        DateTimeOffset.UtcNow));
        }
    }
}