namespace Kwy.Communicate.Gem;

public sealed record GemValidationResult(bool IsValid, string? Error = null)
{
    public static GemValidationResult Success { get; } = new(true);

    public static GemValidationResult Failure(string error) => new(false, error);
}

public interface IGemValidator<in T>
{
    GemValidationResult Validate(T value);
}

/// <summary>集中描述 Host Primary Message 可以在哪些会话状态下执行。</summary>
public sealed record GemSessionPolicy(
    bool RequireCommunicating = true,
    IReadOnlySet<GemControlState>? AllowedControlStates = null)
{
    public GemValidationResult Validate(GemCommunicationContext context)
    {
        if (RequireCommunicating && context.CommunicationState != GemCommunicationState.Communicating)
            return GemValidationResult.Failure("GEM session is not communicating.");
        if (AllowedControlStates is { Count: > 0 } states && !states.Contains(context.ControlState))
            return GemValidationResult.Failure($"Control state {context.ControlState} is not allowed.");
        return GemValidationResult.Success;
    }
}

public sealed record GemEquipmentConstantChange(GemEcid Ecid, Secs4Net.Item Value);

public sealed class GemEquipmentConstantValidator : IGemValidator<IReadOnlyList<GemEquipmentConstantChange>>
{
    private readonly GemInterfaceCatalog catalog;

    public GemEquipmentConstantValidator(GemInterfaceCatalog catalog) => this.catalog = catalog;

    public GemValidationResult Validate(IReadOnlyList<GemEquipmentConstantChange> changes)
    {
        if (changes.Count == 0)
            return GemValidationResult.Failure("At least one equipment constant is required.");
        foreach (GemEquipmentConstantChange change in changes)
        {
            if (!catalog.Constants.TryGetValue(change.Ecid.Value, out GemEquipmentConstantDefinition? definition))
                return GemValidationResult.Failure($"ECID {change.Ecid.Value} is not defined.");
            if (definition.Format is Secs4Net.SecsFormat expected && change.Value.Format != expected)
                return GemValidationResult.Failure(
                    $"ECID {change.Ecid.Value} expects {expected}, but received {change.Value.Format}.");
        }
        return GemValidationResult.Success;
    }
}

public sealed record GemTraceRequest(uint TraceId, TimeSpan SampleInterval, uint TotalSamples, IReadOnlyList<GemVid> VariableIds);

public sealed class GemTraceRequestValidator : IGemValidator<GemTraceRequest>
{
    private readonly GemInterfaceCatalog catalog;

    public GemTraceRequestValidator(GemInterfaceCatalog catalog) => this.catalog = catalog;

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

public sealed class GemProcessProgramValidator : IGemValidator<GemProcessProgram>
{
    public GemValidationResult Validate(GemProcessProgram value)
        => string.IsNullOrWhiteSpace(value.Ppid)
            ? GemValidationResult.Failure("PPID is required.")
            : GemValidationResult.Success;
}
