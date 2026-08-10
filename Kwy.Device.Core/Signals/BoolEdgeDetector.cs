namespace Kwy.Device.Core.Signals;

public sealed class BoolEdgeDetector
{
    private bool initialized;
    private bool previous;

    public BoolEdgeDetector()
    {
    }

    public BoolEdgeDetector(bool initialValue)
    {
        Reset(initialValue);
    }

    public bool IsInitialized => initialized;

    public bool Previous => previous;

    public SignalEdge Update(bool current)
    {
        if (!initialized)
        {
            initialized = true;
            previous = current;
            return SignalEdge.None;
        }

        SignalEdge edge = (previous, current) switch
        {
            (false, true) => SignalEdge.Rising,
            (true, false) => SignalEdge.Falling,
            _ => SignalEdge.None
        };

        previous = current;
        return edge;
    }

    public bool IsTriggered(bool current, EdgeTriggerMode mode)
    {
        SignalEdge edge = Update(current);
        return mode switch
        {
            EdgeTriggerMode.Rising => edge == SignalEdge.Rising,
            EdgeTriggerMode.Falling => edge == SignalEdge.Falling,
            EdgeTriggerMode.Changed => edge is SignalEdge.Rising or SignalEdge.Falling,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public void Reset()
    {
        initialized = false;
        previous = false;
    }

    public void Reset(bool initialValue)
    {
        initialized = true;
        previous = initialValue;
    }
}
