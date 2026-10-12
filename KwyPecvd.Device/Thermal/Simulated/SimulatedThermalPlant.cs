namespace KwyPecvd.Device.Thermal.Simulated;

/// <summary>
/// 仿真加热区域的共享热环境。
/// 不属于真实硬件抽象。
/// </summary>
public sealed class SimulatedThermalPlant
{
    private readonly object syncRoot = new();

    private readonly double ambientTemperature;

    private readonly double heatingRatePerSecond;

    private readonly double passiveRatePerSecond;

    private double temperature;

    private bool heaterPowerOn;

    private bool heatingBlocked;

    private DateTimeOffset lastUpdatedAt;

    public SimulatedThermalPlant(
        double ambientTemperature = 25,
        double initialTemperature = 25,
        double heatingRatePerSecond = 20,
        double passiveRatePerSecond = 5)
    {
        if (!double.IsFinite(ambientTemperature))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ambientTemperature));
        }

        if (!double.IsFinite(initialTemperature))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialTemperature));
        }

        if (!double.IsFinite(heatingRatePerSecond) ||
            heatingRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heatingRatePerSecond));
        }

        if (!double.IsFinite(passiveRatePerSecond) ||
            passiveRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passiveRatePerSecond));
        }

        this.ambientTemperature =
            ambientTemperature;

        this.heatingRatePerSecond =
            heatingRatePerSecond;

        this.passiveRatePerSecond =
            passiveRatePerSecond;

        temperature = initialTemperature;
        lastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public double ReadTemperature(
    DateTimeOffset now)
    {
        lock (syncRoot)
        {
            Advance(now);
            return temperature;
        }
    }

    public void SetHeaterPower(
        bool enabled,
        DateTimeOffset now)
    {
        lock (syncRoot)
        {
            // 先用旧功率推进到当前时刻，
            // 再改变功率状态。
            Advance(now);

            heaterPowerOn = enabled;
        }
    }
    public void SetHeatingBlocked(
    bool value,
    DateTimeOffset now)
    {
        lock (syncRoot)
        {
            Advance(now);

            heatingBlocked = value;
        }
    }

    private void Advance(
    DateTimeOffset now)
    {
        var elapsedSeconds =
            Math.Max(
                0,
                (now - lastUpdatedAt)
                    .TotalSeconds);

        lastUpdatedAt = now;

        if (elapsedSeconds <= 0)
        {
            return;
        }

        if (heaterPowerOn && !heatingBlocked)
        {
            temperature +=
                heatingRatePerSecond *
                elapsedSeconds;

            return;
        }

        MoveTowardAmbient(elapsedSeconds);
    }

    private void MoveTowardAmbient(
        double elapsedSeconds)
    {
        var maximumChange =
            passiveRatePerSecond *
            elapsedSeconds;

        var difference =
            ambientTemperature -
            temperature;

        if (Math.Abs(difference) <= maximumChange)
        {
            temperature = ambientTemperature;
            return;
        }

        temperature +=
            Math.Sign(difference) *
            maximumChange;
    }
}