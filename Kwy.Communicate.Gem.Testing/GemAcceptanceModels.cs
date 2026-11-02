using System.Text;
using System.Text.Json;

namespace Kwy.Communicate.Gem.Testing;

public enum GemAcceptanceStatus
{
    Passed,
    Failed,
    Skipped
}

public sealed record GemAcceptanceStepResult(
    string Name,
    GemAcceptanceStatus Status,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    string? Detail = null,
    string? ErrorType = null);

public sealed record GemAcceptanceReport(
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<GemAcceptanceStepResult> Steps)
{
    public bool Passed => Steps.Count > 0 && Steps.All(x => x.Status == GemAcceptanceStatus.Passed);

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = indented });

    public string ToMarkdown()
    {
        var text = new StringBuilder()
            .AppendLine($"# GEM acceptance report: {Name}")
            .AppendLine()
            .AppendLine($"- Result: **{(Passed ? "PASS" : "FAIL")}**")
            .AppendLine($"- Started: {StartedAt:O}")
            .AppendLine($"- Finished: {FinishedAt:O}")
            .AppendLine()
            .AppendLine("| Step | Result | Duration | Detail |")
            .AppendLine("|---|---:|---:|---|");

        foreach (GemAcceptanceStepResult step in Steps)
        {
            string detail = (step.Detail ?? string.Empty).Replace("|", "\\|").ReplaceLineEndings(" ");
            text.AppendLine($"| {step.Name} | {step.Status} | {step.Duration.TotalMilliseconds:F0} ms | {detail} |");
        }

        return text.ToString();
    }
}

public sealed class GemAcceptanceContext
{
    private readonly Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

    public GemAcceptanceContext(IGemHostTransport transport, TimeSpan? stepTimeout = null)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        StepTimeout = stepTimeout ?? TimeSpan.FromSeconds(30);
        if (StepTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(stepTimeout));
    }

    public IGemHostTransport Transport { get; }

    public TimeSpan StepTimeout { get; }

    public void Set<T>(string key, T value) => values[key] = value;

    public T? Get<T>(string key)
        => values.TryGetValue(key, out object? value) && value is T typed ? typed : default;
}

public sealed record GemAcceptanceStep(
    string Name,
    Func<GemAcceptanceContext, CancellationToken, Task<string?>> ExecuteAsync);
