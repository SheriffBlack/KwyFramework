namespace KwyTemplate.Flow.Models;

/// <summary>
/// 将可能清零或回绕的 PLC 原始计数投影为本次 PC 运行期的单调累计值。
/// </summary>
internal sealed class CumulativePlcCounter
{
    private readonly object syncRoot = new();
    private bool initialized;
    private bool discardNextObservation;
    private uint previousRawValue;
    private ulong cumulativeValue;

    public uint Observe(uint rawValue)
    {
        lock (syncRoot)
        {
            if (!initialized)
            {
                initialized = true;
                previousRawValue = rawValue;
                cumulativeValue = rawValue;
                return rawValue;
            }

            if (discardNextObservation)
            {
                discardNextObservation = false;
                previousRawValue = rawValue;
                return 0;
            }

            cumulativeValue += rawValue >= previousRawValue
                ? rawValue - previousRawValue
                : rawValue;
            previousRawValue = rawValue;
            return cumulativeValue > uint.MaxValue
                ? uint.MaxValue
                : (uint)cumulativeValue;
        }
    }

    public void Reset()
    {
        lock (syncRoot)
        {
            initialized = true;
            discardNextObservation = true;
            previousRawValue = 0;
            cumulativeValue = 0;
        }
    }
}
