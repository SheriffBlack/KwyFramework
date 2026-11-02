namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public Task<GemTraceSample> CaptureTraceAsync(
        uint traceId,
        uint sampleNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(traceService.Capture(traceId, sampleNumber));
    }

    public async Task<GemTraceRunResult> RunTraceAsync(uint traceId, CancellationToken cancellationToken = default)
    {
        if (!registry.Traces.TryGetValue(traceId, out GemTraceDefinition? trace))
            throw new KeyNotFoundException($"Trace {traceId} is not registered.");
        if (trace.State == GemTraceState.Disabled)
            throw new InvalidOperationException($"Trace {traceId} is disabled.");
        if (trace.TotalSamples == 0)
            throw new InvalidOperationException($"Trace {traceId} must contain at least one sample.");
        if (trace.SampleInterval < TimeSpan.Zero)
            throw new InvalidOperationException($"Trace {traceId} sample interval cannot be negative.");

        DateTimeOffset startedAt = DateTimeOffset.Now;
        uint reported = 0;
        try
        {
            for (uint sampleNumber = 1; sampleNumber <= trace.TotalSamples; sampleNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GemTraceSample sample = traceService.Capture(traceId, sampleNumber);
                await SendPrimaryOrSpoolAsync(GemMessageFactory.TraceDataSend(sample), cancellationToken)
                    .ConfigureAwait(false);
                reported++;
                if (sampleNumber < trace.TotalSamples && trace.SampleInterval > TimeSpan.Zero)
                    await Task.Delay(trace.SampleInterval, cancellationToken).ConfigureAwait(false);
            }
            return new(traceId, reported, GemTraceState.Completed, startedAt, DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new(traceId, reported, GemTraceState.Cancelled, startedAt, DateTimeOffset.Now);
        }
    }
}
