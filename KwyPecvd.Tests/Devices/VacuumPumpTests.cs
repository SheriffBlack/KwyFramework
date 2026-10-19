using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Tests.Devices;

public sealed class VacuumPumpTests
{
    [Fact]
    public async Task StartAsync_WritesImmediatelyAndCyclesDoNotRewriteCommand()
    {
        var pump = new TestVacuumPump();
        await pump.InitializeAsync();
        await pump.ExecuteCycleAsync();
        int writesAfterInitialize = pump.WriteCount;

        var result = await pump.StartAsync();

        Assert.True(result.Succeeded);
        Assert.True(pump.LastWrittenRunning);
        Assert.Equal(writesAfterInitialize + 1, pump.WriteCount);

        await pump.ExecuteCycleAsync();
        await pump.ExecuteCycleAsync();

        Assert.Equal(writesAfterInitialize + 1, pump.WriteCount);
        Assert.True(pump.Snapshot.IsRunning);
    }

    [Fact]
    public async Task ExecuteCycleAsync_WhenAlarmAppears_WritesStopImmediately()
    {
        var pump = new TestVacuumPump();
        await pump.InitializeAsync();
        await pump.ExecuteCycleAsync();
        await pump.StartAsync();
        pump.HasAlarm = true;

        await pump.ExecuteCycleAsync();

        Assert.False(pump.LastWrittenRunning);
        Assert.False(pump.Snapshot.CommandedOn);
        Assert.True(pump.Snapshot.HasAlarm);
    }

    private sealed class TestVacuumPump() : VacuumPumpBase(
        new VacuumPumpDefinition
        {
            Id = "PUMP_PM1_TEST",
            ModuleId = "PM1",
            DisplayName = "Test vacuum pump"
        })
    {
        public int WriteCount { get; private set; }
        public bool LastWrittenRunning { get; private set; }
        public bool HasAlarm { get; set; }

        protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            WriteCount++;
            LastWrittenRunning = false;
            return Task.CompletedTask;
        }

        protected override ValueTask WriteRunningAsync(
            bool running,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastWrittenRunning = running;
            WriteCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<VacuumPumpCycleResult> ReadFeedbackAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new VacuumPumpCycleResult(
                    IsRunning: LastWrittenRunning && !HasAlarm,
                    HasWarning: false,
                    HasAlarm: HasAlarm,
                    IsOverloaded: false,
                    IsN2Ready: true,
                    IsExhaustReady: true,
                    IsOffline: false,
                    IsFeedbackValid: true,
                    Timestamp: DateTimeOffset.UtcNow));
        }
    }
}
