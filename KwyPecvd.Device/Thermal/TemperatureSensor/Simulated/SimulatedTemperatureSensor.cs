using KwyPecvd.Device.Thermal.Simulated;

namespace KwyPecvd.Device.Thermal.TemperatureSensor.Simulated;

public sealed class SimulatedTemperatureSensor : TemperatureSensorBase
{
    private readonly object simulationSync = new();

    private readonly SimulatedThermalPlant plant;

    private bool isOffline;

    private bool hasBadQuality;

    public SimulatedTemperatureSensor(
        TemperatureSensorDefinition definition,
        SimulatedThermalPlant plant)
        : base(definition)
    {
        this.plant =
            plant ??
            throw new ArgumentNullException(
                nameof(plant));
    }

    protected override ValueTask<TemperatureSensorCycleResult> ReadTemperatureAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            var temperature =
                plant.ReadTemperature(now);

            var readingValid =
                !isOffline &&
                !hasBadQuality;

            return ValueTask.FromResult(
                new TemperatureSensorCycleResult(
                    Temperature: temperature,
                    IsOffline: isOffline,
                    IsReadingValid: readingValid,
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

    public void SetBadQuality(bool value)
    {
        lock (simulationSync)
        {
            hasBadQuality = value;
        }
    }
}