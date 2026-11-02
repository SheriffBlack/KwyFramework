using System.Runtime.CompilerServices;
using Kwy.Communicate.Gem;
using Kwy.Communicate.Gem.Testing;
using Secs4Net;

namespace Kwy.Communicate.Tests;

public sealed class GemAcceptancePlanTests
{
    [Fact]
    public async Task RunAsync_ProducesAuditableReport()
    {
        await using var transport = new FakeHostTransport
        {
            Reply = new SecsMessage(1, 18) { SecsItem = Item.B(0) }
        };
        var plan = new GemAcceptancePlan("smoke")
            .Add(GemAcceptanceSteps.Connect())
            .Add(GemAcceptanceSteps.RequestOnline());

        GemAcceptanceReport report = await plan.RunAsync(new(transport));

        Assert.True(report.Passed);
        Assert.All(report.Steps, step => Assert.Equal(GemAcceptanceStatus.Passed, step.Status));
        Assert.Contains("\"Passed\": true", report.ToJson());
        Assert.Contains("| Request Online | Passed |", report.ToMarkdown());
    }

    [Fact]
    public async Task RunAsync_FailsWrongSecondaryAndSkipsFollowingSteps()
    {
        await using var transport = new FakeHostTransport
        {
            Reply = new SecsMessage(1, 4) { SecsItem = Item.L() }
        };
        var plan = new GemAcceptancePlan("negative")
            .Add(GemAcceptanceSteps.RequestOnline())
            .Add(GemAcceptanceSteps.Disconnect());

        GemAcceptanceReport report = await plan.RunAsync(new(transport));

        Assert.False(report.Passed);
        Assert.Equal(GemAcceptanceStatus.Failed, report.Steps[0].Status);
        Assert.Equal(GemAcceptanceStatus.Skipped, report.Steps[1].Status);
        Assert.Contains("expected S1F18", report.Steps[0].Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReportsStepTimeout()
    {
        await using var transport = new FakeHostTransport();
        var plan = new GemAcceptancePlan("timeout")
            .Add(GemAcceptanceSteps.Custom("wait", async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return null;
            }));

        GemAcceptanceReport report = await plan.RunAsync(
            new(transport, TimeSpan.FromMilliseconds(20)));

        Assert.False(report.Passed);
        Assert.Contains("timeout", report.Steps[0].Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeHostTransport : IGemHostTransport
    {
        public bool IsConnected { get; private set; }

        public SecsMessage? Reply { get; init; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(Reply);

        public async IAsyncEnumerable<PrimaryMessageWrapper> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
