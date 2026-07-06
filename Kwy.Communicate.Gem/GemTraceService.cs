using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Gem;

public sealed class GemTraceService
{
    private readonly GemRegistry registry;

    public GemTraceService(GemRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void RegisterTrace(GemTraceDefinition trace) => registry.RegisterTrace(trace);

    public GemTraceSample Capture(uint traceId, uint sampleNumber)
    {
        if (!registry.Traces.TryGetValue(traceId, out var trace))
        {
            throw new KeyNotFoundException($"Trace {traceId} is not registered.");
        }

        var values = new Dictionary<GemVid, SecsItem>();
        foreach (GemVid vid in trace.VariableIds)
        {
            values[vid] = registry.Variables.TryGetValue(vid.Value, out var variable)
                ? variable.Value
                : SecsItem.A(string.Empty);
        }

        var sample = new GemTraceSample(traceId, sampleNumber, DateTimeOffset.Now, values);
        registry.AddTraceSample(sample);
        return sample;
    }
}
