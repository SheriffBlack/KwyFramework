namespace KwyTemplate.Flow.Services;

/// <summary>
/// Online accumulator for CPK calculation using Welford variance.
/// </summary>
public sealed class MeasurementCpkAccumulator
{
    private readonly object sync = new();
    private int count;
    private double mean;
    private double m2;

    public void Add(double value)
    {
        lock (sync)
        {
            count++;
            double delta = value - mean;
            mean += delta / count;
            double delta2 = value - mean;
            m2 += delta * delta2;
        }
    }

    public void Reset()
    {
        lock (sync)
        {
            count = 0;
            mean = 0;
            m2 = 0;
        }
    }

    public double? CalculateCpk(double lowerLimit, double upperLimit)
    {
        lock (sync)
        {
            if (count < 2)
            {
                return null;
            }

            double variance = m2 / (count - 1);
            if (variance <= 0)
            {
                return null;
            }

            double sigma = Math.Sqrt(variance);
            double cpu = (upperLimit - mean) / (3 * sigma);
            double cpl = (mean - lowerLimit) / (3 * sigma);
            return Math.Min(cpu, cpl);
        }
    }
}
