using KwyPecvd.Device.RfMatcher;
using KwyPecvd.Device.RfMatcher.Simulated;

namespace KwyPecvd.Tests.Devices;

public sealed class SimulatedRfMatcherTests
{
    [Fact]
    public async Task ManualMode_MovesTowardCommandedPositions()
    {
        var matcher = CreateMatcher(
            responseFactor: 0.5);

        await matcher.InitializeAsync();
        await matcher.ExecuteCycleAsync();

        var modeResult =
            await matcher.SetModeAsync(
                RfMatcherMode.Manual);

        var positionResult =
            await matcher.SetManualPositionAsync(
                tunePosition: 30,
                loadPosition: 70);

        await matcher.ExecuteCycleAsync();

        Assert.True(modeResult.Succeeded);
        Assert.True(positionResult.Succeeded);

        Assert.Equal(
            RfMatcherMode.Manual,
            matcher.Snapshot.CommandedMode);

        Assert.Equal(
            RfMatcherMode.Manual,
            matcher.Snapshot.ActualMode);

        Assert.Equal(
            15,
            matcher.Snapshot.TunePosition);

        Assert.Equal(
            35,
            matcher.Snapshot.LoadPosition);

        Assert.False(
            matcher.Snapshot.IsTuning);

        Assert.False(
            matcher.Snapshot.IsMatched);
    }

    [Fact]
    public async Task AutoMode_TransitionsFromTuningToMatched()
    {
        var matcher = CreateMatcher(
            responseFactor: 0.5,
            automaticTuneTarget: 60,
            automaticLoadTarget: 40);

        await matcher.InitializeAsync();
        await matcher.ExecuteCycleAsync();

        await matcher.SetModeAsync(
            RfMatcherMode.Auto);

        await matcher.ExecuteCycleAsync();

        Assert.Equal(
            RfMatcherMode.Auto,
            matcher.Snapshot.ActualMode);

        Assert.True(
            matcher.Snapshot.IsTuning);

        Assert.False(
            matcher.Snapshot.IsMatched);

        // 继续运行若干硬件周期，
        // 让Tune和Load接近自动匹配目标。
        for (var index = 0;
             index < 10;
             index++)
        {
            await matcher.ExecuteCycleAsync();
        }

        Assert.False(
            matcher.Snapshot.IsTuning);

        Assert.True(
            matcher.Snapshot.IsMatched);

        Assert.InRange(
            matcher.Snapshot.TunePosition,
            59,
            61);

        Assert.InRange(
            matcher.Snapshot.LoadPosition,
            39,
            41);
    }

    private static SimulatedRfMatcher
        CreateMatcher(
            double responseFactor = 0.2,
            double automaticTuneTarget = 60,
            double automaticLoadTarget = 40)
    {
        return new SimulatedRfMatcher(
            new RfMatcherDefinition
            {
                Id = "MATCH_PM1",
                ModuleId = "PM1",
                DisplayName =
                    "PM1 RF Matcher",
                PositionUnit = "%",
                MinimumPosition = 0,
                MaximumPosition = 100,
                PositionTolerance = 1,
                PositionToleranceDelay =
                    TimeSpan.FromSeconds(3)
            },
            responseFactor,
            automaticTuneTarget,
            automaticLoadTarget);
    }
}