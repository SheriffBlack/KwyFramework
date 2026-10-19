using KwyPecvd.Device.OnOffValve;

namespace KwyPecvd.Tests.Devices;

public sealed class OnOffValveTests
{
    [Fact]
    public async Task OpenAsync_WritesImmediatelyAndCyclesDoNotRewriteCommand()
    {
        var valve = new TestOnOffValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();
        int writesAfterInitialize = valve.WriteCount;

        var result = await valve.OpenAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(OnOffValvePosition.Open, valve.LastWrittenPosition);
        Assert.Equal(writesAfterInitialize + 1, valve.WriteCount);
        Assert.Equal(OnOffValveState.Opening, valve.Snapshot.State);

        await valve.ExecuteCycleAsync();
        await valve.ExecuteCycleAsync();

        Assert.Equal(writesAfterInitialize + 1, valve.WriteCount);
        Assert.Equal(OnOffValveState.Open, valve.Snapshot.State);
    }

    [Fact]
    public async Task ExecuteCycleAsync_WhenUnsafeFeedbackFails_WritesSafePosition()
    {
        var valve = new TestOnOffValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();
        await valve.OpenAsync();
        valve.IsOffline = true;

        await valve.ExecuteCycleAsync();

        Assert.Equal(OnOffValvePosition.Closed, valve.LastWrittenPosition);
        Assert.Equal(OnOffValvePosition.Closed, valve.Snapshot.CommandedPosition);
        Assert.True(valve.Snapshot.IsOffline);
    }

    private sealed class TestOnOffValve() : OnOffValveBase(
        new OnOffValveDefinition
        {
            Id = "VALVE_PM1_TEST",
            ModuleId = "PM1",
            DisplayName = "Test valve",
            SafePosition = OnOffValvePosition.Closed,
            MotionTimeout = TimeSpan.FromSeconds(5)
        })
    {
        public int WriteCount { get; private set; }
        public OnOffValvePosition LastWrittenPosition { get; private set; } =
            OnOffValvePosition.Closed;
        public bool IsOffline { get; set; }

        protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            LastWrittenPosition = Definition.SafePosition;
            WriteCount++;
            return Task.CompletedTask;
        }

        protected override ValueTask WritePositionAsync(
            OnOffValvePosition position,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastWrittenPosition = position;
            WriteCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<OnOffValveCycleResult> ReadFeedbackAsync(
            OnOffValvePosition commandedPosition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new OnOffValveCycleResult(
                    ActualPosition: IsOffline ? null : LastWrittenPosition,
                    IsOffline: IsOffline,
                    IsFeedbackValid: !IsOffline,
                    IsFeedbackConflict: false,
                    Timestamp: DateTimeOffset.UtcNow));
        }
    }
}
