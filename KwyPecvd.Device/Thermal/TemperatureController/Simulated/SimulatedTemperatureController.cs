using KwyPecvd.Device.Thermal.Simulated;

namespace KwyPecvd.Device.Thermal.TemperatureController.Simulated;

public sealed class SimulatedTemperatureController : TemperatureControllerBase
{
    private readonly object simulationSync = new();

    private readonly SimulatedThermalPlant plant;

    private readonly double controlDeadband;

    private bool isPowerOn;

    private bool isOffline;

    private bool hasBadQuality;

    public SimulatedTemperatureController(
        TemperatureControllerDefinition definition,
        SimulatedThermalPlant plant,
        double controlDeadband = 1)
        : base(definition)
    {
        ArgumentNullException.ThrowIfNull(plant);

        if (!double.IsFinite(controlDeadband) ||
            controlDeadband < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(controlDeadband));
        }

        this.plant = plant;
        this.controlDeadband = controlDeadband;
    }

    protected override ValueTask<TemperatureControllerCycleResult> ExchangeAsync(
        double target,
        bool enabled,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            var feedback =
                plant.ReadTemperature(now);

            if (isOffline)
            {
                isPowerOn = false;

                plant.SetHeaterPower(
                    enabled: false,
                    now);

                return ValueTask.FromResult(
                    new TemperatureControllerCycleResult(
                        Feedback: feedback,
                        IsPowerOn: false,
                        IsOffline: true,
                        IsFeedbackValid: false,
                        Timestamp: now));
            }

            if (hasBadQuality)
            {
                isPowerOn = false;

                plant.SetHeaterPower(
                    enabled: false,
                    now);

                return ValueTask.FromResult(
                    new TemperatureControllerCycleResult(
                        Feedback: feedback,
                        IsPowerOn: false,
                        IsOffline: false,
                        IsFeedbackValid: false,
                        Timestamp: now));
            }

            UpdatePowerState(
                target,
                enabled,
                feedback);

            plant.SetHeaterPower(
                isPowerOn,
                now);

            return ValueTask.FromResult(
                new TemperatureControllerCycleResult(
                    Feedback: feedback,
                    IsPowerOn: isPowerOn,
                    IsOffline: false,
                    IsFeedbackValid: true,
                    Timestamp: now));
        }
    }

   
    private void UpdatePowerState(
        double target,
        bool enabled,
        double feedback)
    {
        if (!enabled)
        {
            isPowerOn = false;
            return;
        }

        if (isPowerOn)
        {
            if (feedback >=
                target + controlDeadband)
            {
                isPowerOn = false;
            }

            return;
        }

        if (feedback <=
            target - controlDeadband)
        {
            isPowerOn = true;
        }
    }

    public void SetOffline(bool value)
    {
        lock (simulationSync)
        {
            isOffline = value;

            if (value)
            {
                isPowerOn = false;

                plant.SetHeaterPower(
                    false,
                    DateTimeOffset.UtcNow);
            }
        }
    }

    public void SetBadQuality(bool value)
    {
        lock (simulationSync)
        {
            hasBadQuality = value;

            if (value)
            {
                isPowerOn = false;

                plant.SetHeaterPower(
                    false,
                    DateTimeOffset.UtcNow);
            }
        }
    }

    public void SetHeatingBlocked(bool value)
    {
        plant.SetHeatingBlocked(
            value,
            DateTimeOffset.UtcNow);
    }
}