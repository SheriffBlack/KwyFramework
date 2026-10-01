using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class GemTraceService
{
    private readonly GemRegistry registry;

    public GemTraceService(GemRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void RegisterTrace(GemTraceDefinition trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (trace.TraceId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trace), "Trace ID must be greater than zero.");
        }

        if (trace.TotalSamples == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trace), "A trace must contain at least one sample.");
        }

        if (trace.SampleInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(trace), "Trace sample interval cannot be negative.");
        }

        if (trace.VariableIds.Count == 0)
        {
            throw new ArgumentException("A trace must contain at least one variable.", nameof(trace));
        }

        registry.RegisterTrace(trace);
    }

    public GemTraceSample Capture(uint traceId, uint sampleNumber)
    {
        if (!registry.Traces.TryGetValue(traceId, out var trace))
        {
            throw new KeyNotFoundException($"Trace {traceId} is not registered.");
        }

        IReadOnlyDictionary<GemVid, GemVariable> snapshot = registry.Data.CaptureRequired(trace.VariableIds);
        var values = snapshot.ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        var sample = new GemTraceSample(traceId, sampleNumber, DateTimeOffset.Now, values);
        registry.AddTraceSample(sample);
        return sample;
    }
}
