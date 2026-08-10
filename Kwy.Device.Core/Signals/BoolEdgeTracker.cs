namespace Kwy.Device.Core.Signals;

public sealed class BoolEdgeTracker<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, BoolEdgeDetector> detectors = [];

    public SignalEdge Update(TKey key, bool current)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetOrCreate(key).Update(current);
    }

    public bool IsTriggered(TKey key, bool current, EdgeTriggerMode mode)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetOrCreate(key).IsTriggered(current, mode);
    }

    public void Reset()
    {
        detectors.Clear();
    }

    public void Reset(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        detectors.Remove(key);
    }

    public void Reset(TKey key, bool initialValue)
    {
        ArgumentNullException.ThrowIfNull(key);
        GetOrCreate(key).Reset(initialValue);
    }

    private BoolEdgeDetector GetOrCreate(TKey key)
    {
        if (!detectors.TryGetValue(key, out BoolEdgeDetector? detector))
        {
            detector = new BoolEdgeDetector();
            detectors.Add(key, detector);
        }

        return detector;
    }
}
