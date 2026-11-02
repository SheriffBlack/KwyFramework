using System.Diagnostics;

namespace Kwy.Communicate.Gem.Testing;

/// <summary>顺序执行 Host 验收步骤，并生成可审核的结构化结果。</summary>
public sealed class GemAcceptancePlan
{
    private readonly List<GemAcceptanceStep> steps = [];

    public GemAcceptancePlan(string name)
        => Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Plan name is required.", nameof(name)) : name;

    public string Name { get; }

    public bool StopOnFailure { get; init; } = true;

    public IReadOnlyList<GemAcceptanceStep> Steps => steps;

    public GemAcceptancePlan Add(GemAcceptanceStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (steps.Any(x => string.Equals(x.Name, step.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Acceptance step '{step.Name}' is duplicated.");
        steps.Add(step);
        return this;
    }

    public async Task<GemAcceptanceReport> RunAsync(
        GemAcceptanceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        var results = new List<GemAcceptanceStepResult>(steps.Count);
        bool failed = false;

        foreach (GemAcceptanceStep step in steps)
        {
            if (failed && StopOnFailure)
            {
                results.Add(new(step.Name, GemAcceptanceStatus.Skipped, DateTimeOffset.UtcNow, TimeSpan.Zero,
                    "Skipped because an earlier step failed."));
                continue;
            }

            DateTimeOffset stepStartedAt = DateTimeOffset.UtcNow;
            long timestamp = Stopwatch.GetTimestamp();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(context.StepTimeout);
            try
            {
                string? detail = await step.ExecuteAsync(context, timeout.Token).ConfigureAwait(false);
                results.Add(new(step.Name, GemAcceptanceStatus.Passed, stepStartedAt,
                    Stopwatch.GetElapsedTime(timestamp), detail));
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failed = true;
                string detail = exception is OperationCanceledException
                    ? $"Step exceeded timeout {context.StepTimeout}."
                    : exception.Message;
                results.Add(new(step.Name, GemAcceptanceStatus.Failed, stepStartedAt,
                    Stopwatch.GetElapsedTime(timestamp), detail, exception.GetType().FullName));
            }
        }

        return new(Name, startedAt, DateTimeOffset.UtcNow, results);
    }
}
