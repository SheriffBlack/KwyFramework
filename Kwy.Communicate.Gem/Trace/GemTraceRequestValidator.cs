namespace Kwy.Communicate.Gem;

public sealed record GemTraceRequest(
    uint TraceId,
    TimeSpan SampleInterval,
    uint TotalSamples,
    IReadOnlyList<GemVid> VariableIds);

public sealed class GemTraceRequestValidator : IGemValidator<GemTraceRequest>
{
    private readonly IGemInterfaceCatalog catalog;

    public GemTraceRequestValidator(IGemInterfaceCatalog catalog)
        => this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public GemValidationResult Validate(GemTraceRequest value)
    {
        if (value.TraceId == 0 || value.TotalSamples == 0 || value.SampleInterval < TimeSpan.Zero || value.VariableIds.Count == 0)
            return GemValidationResult.Failure("Trace ID, sample count, interval or SVID list is invalid.");
        foreach (GemVid id in value.VariableIds)
        {
            if (!catalog.Variables.ContainsKey(id.Value))
                return GemValidationResult.Failure($"VID {id.Value} is not defined.");
        }
        return GemValidationResult.Success;
    }
}
