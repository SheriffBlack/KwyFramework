using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Process.Chambers;

namespace KwyPecvd.Tests.Process;

public sealed class ChamberConcurrencyTests
{
    [Fact]
    public async Task PreparationInFlight_RejectsStopUntilCommandCompletes()
    {
        var (chamber, first, _) = CreateChamber();
        await chamber.InitializeAsync();
        first.SetBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation = chamber.StartGasPreparationAsync(Targets());
        var stop = await chamber.StopProcessAsync();
        Assert.False(stop.Succeeded);
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Preparing, chamber.Snapshot.State);
        first.SetBarrier.SetResult();
        Assert.True((await preparation).Succeeded);
        Assert.True((await chamber.StopProcessAsync()).Succeeded);
        Assert.Equal(0, first.Target);
    }

    [Fact]
    public async Task StopInFlight_DoesNotCompleteFromOldFeedback_AndIgnoresRequestCancellation()
    {
        var (chamber, first, second) = CreateChamber();
        await chamber.InitializeAsync();
        await chamber.StartGasPreparationAsync(Targets());
        first.StopBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var request = new CancellationTokenSource();
        var stop = chamber.StopProcessAsync(request.Token);
        request.Cancel();
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Stopping, chamber.Snapshot.State);
        first.StopBarrier.SetResult();
        Assert.True((await stop).Succeeded);
        Assert.Equal(1, second.StopCount);
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Stopping, chamber.Snapshot.State);
        await first.ExecuteCycleAsync();
        await second.ExecuteCycleAsync();
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Idle, chamber.Snapshot.State);
    }

    [Fact]
    public async Task PreparationWaiting_DoesNotHideLaterOfflineGas()
    {
        var (chamber, first, second) = CreateChamber();
        await chamber.InitializeAsync();
        await chamber.StartGasPreparationAsync(Targets());
        first.Current = first.Current with { IsRamping = true };
        second.Current = second.Current with { IsOffline = true };
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Faulted, chamber.Snapshot.State);
        Assert.Equal(
            ChamberFaultCodes.GasMfcOffline,
            chamber.Snapshot.FaultCode);
    }

    [Fact]
    public async Task StopWaiting_DoesNotHideLaterInvalidFeedback_AndStopsUnusedGas()
    {
        var (chamber, first, second) = CreateChamber();
        await chamber.InitializeAsync();
        await chamber.StartGasPreparationAsync([Targets()[0]]);
        await chamber.StopProcessAsync();
        Assert.Equal(1, second.StopCount);
        first.Current = first.Current with { Feedback = 100 };
        second.Current = second.Current with { IsFeedbackValid = false };
        await chamber.ExecuteCycleAsync();
        Assert.Equal(ChamberState.Faulted, chamber.Snapshot.State);
        Assert.Equal(
            ChamberFaultCodes.GasFeedbackInvalid,
            chamber.Snapshot.FaultCode);
    }

    private static ChamberGasTarget[] Targets() =>
    [
        new() { GasName = "SiH4", Target = 100 },
        new() { GasName = "N2", Target = 200 }
    ];

    private static (ChamberController, TestMfc, TestMfc) CreateChamber()
    {
        var registry =
            new HardwareComponentRegistry();

        var first = new TestMfc("SiH4");
        var second = new TestMfc("N2");

        registry.Add(first);
        registry.Add(second);

        var definition =
            ChamberTestFixture.CreateDefinition(
                registry,
                [
                    new ChamberGasLineDefinition
                {
                    GasName = "SiH4",
                    MfcId = first.Id
                },
                new ChamberGasLineDefinition
                {
                    GasName = "N2",
                    MfcId = second.Id
                }
                ],
                displayName: "PM1");

        var chamber =
            new ChamberController(
                definition,
                registry,
                ChamberTestFixture
                    .CreateHealthyInterlock());

        return (
            chamber,
            first,
            second);
    }

    private sealed class TestMfc : IMfc
    {
        public TestMfc(string gas)
        {
            Definition = new MfcDefinition
            {
                Id = "MFC_" + gas,
                ModuleId = "PM1",
                DisplayName = gas,
                GasName = gas,
                Unit = "sccm",
                FullScale = 500,
                Tolerance = 2
            };
            Current = new MfcSnapshot
            {
                Id = Id,
                Unit = "sccm",
                FullScale = 500,
                IsFeedbackValid = true,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        public string Id => Definition.Id;
        public MfcDefinition Definition { get; }
        public MfcSnapshot Current { get; set; }
        public MfcSnapshot Snapshot => Current;
        public double Target { get; private set; }
        public int StopCount { get; private set; }
        public TaskCompletionSource? SetBarrier { get; set; }
        public TaskCompletionSource? StopBarrier { get; set; }

        public event EventHandler<MfcSnapshot>? SnapshotChanged
        { add { } remove { } }

        public async Task<CommandResult> SetFlowAsync(double target, TimeSpan rampDuration,
            CancellationToken cancellationToken = default)
        {
            if (SetBarrier is not null)
                await SetBarrier.Task.WaitAsync(cancellationToken);
            Target = target;
            return CommandResult.Success();
        }

        public async Task<CommandResult> StopAsync(CancellationToken cancellationToken = default)
        {
            if (StopBarrier is not null)
                await StopBarrier.Task.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            Target = 0;
            return CommandResult.Success();
        }

        public Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
        {
            Current = Current with
            {
                SetPoint = Target,
                Feedback = Target,
                Timestamp = DateTimeOffset.UtcNow
            };
            return Task.CompletedTask;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CommandResult> HoldAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CommandResult.Success());
    }
}