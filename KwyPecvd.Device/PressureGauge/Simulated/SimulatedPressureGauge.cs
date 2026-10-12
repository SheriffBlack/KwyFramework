using KwyPecvd.Device.PressureGauge;

public sealed class SimulatedPressureGauge :
    PressureGaugeBase
{
    private readonly object simulationSync = new();

    private double pressure = 101_325;

    public SimulatedPressureGauge(PressureGaugeDefinition definition)
        : base(definition)
    {
    }

    public void SetSimulatedPressure(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Pressure must be finite.");
        }

        lock (simulationSync)
        {
            pressure = value;
        }
    }

    protected override ValueTask<PressureGaugeCycleResult> ReadPressureAsync(
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        double currentPressure;

        lock (simulationSync)
        {
            currentPressure = pressure;
        }

        return ValueTask.FromResult(
            new PressureGaugeCycleResult(
                Pressure: currentPressure,
                IsOffline: false,
                IsReadingValid: true,
                Timestamp:
                    DateTimeOffset.UtcNow));
    }
}