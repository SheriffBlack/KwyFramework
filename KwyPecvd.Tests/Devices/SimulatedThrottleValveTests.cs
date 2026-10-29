using KwyPecvd.Device;
using KwyPecvd.Device.ThrottleValve;
using KwyPecvd.Device.ThrottleValve.Simulated;

namespace KwyPecvd.Tests.Devices;

public sealed class SimulatedThrottleValveTests
{
    private static SimulatedThrottleValve CreateValve(
        double positionResponseFactor = 0.2,
        double pressureResponseFactor = 0.2)
    {
        return new SimulatedThrottleValve(
            new ThrottleValveDefinition
            {
                Id = "TV_PM1",
                ModuleId = "PM1",
                DisplayName =
                    "PM1 Throttle Valve",

                MinimumPosition = 0,
                MaximumPosition = 100,
                PositionUnit = "%",

                MaximumPressure = 10,
                PressureUnit = "Torr",

                PositionTolerance = 1,
                PressureTolerance = 0.05,
                ToleranceDelay =
                    TimeSpan.FromSeconds(3),

                SafeMode =
                    ThrottleValveMode.Closed
            },
            positionResponseFactor,
            pressureResponseFactor);
    }

    [Fact]
    public async Task SetPositionAsync_CommandChangesImmediatelyAndFeedbackAdvancesOnCycle()
    {
        var valve = CreateValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();

        var result =
            await valve.SetPositionAsync(50);

        Assert.True(result.Succeeded);

        Assert.Equal(
            ThrottleValveMode.Position,
            valve.Snapshot.CommandedMode);

        // 命令成功后，真实反馈不会被立即伪造成50。
        Assert.Equal(
            0,
            valve.Snapshot.PositionFeedback);

        await valve.ExecuteCycleAsync();

        Assert.Equal(
            ThrottleValveMode.Position,
            valve.Snapshot.ActualMode);

        // responseFactor为0.2：
        // 0 + (50 - 0) * 0.2 = 10
        Assert.Equal(
            10,
            valve.Snapshot.PositionFeedback,
            precision: 6);
    }

    [Fact]
    public async Task SetPositionAsync_AcceptsWhenOnlyPressureFeedbackIsInvalid()
    {
        var valve = CreateValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();

        valve.SetPressureFeedbackValid(false);

        await valve.ExecuteCycleAsync();

        Assert.True(
            valve.Snapshot
                .IsPositionFeedbackValid);

        Assert.False(
            valve.Snapshot
                .IsPressureFeedbackValid);

        var result =
            await valve.SetPositionAsync(30);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SetPressureAsync_RejectsWhenPressureFeedbackIsInvalid()
    {
        var valve = CreateValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();

        valve.SetPressureFeedbackValid(false);

        await valve.ExecuteCycleAsync();

        var result =
            await valve.SetPressureAsync(2);

        Assert.False(result.Succeeded);

        Assert.Equal(
            ResultCodes.NotReady,
            result.Code);

        Assert.Equal(
            ThrottleValveMode.Closed,
            valve.Snapshot.CommandedMode);
    }

    [Fact]
    public async Task SetPressureAsync_FeedbackAdvancesOnCycle()
    {
        var valve = CreateValve(
            pressureResponseFactor: 0.5);

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();

        var result =
            await valve.SetPressureAsync(4);

        Assert.True(result.Succeeded);

        Assert.Equal(
            ThrottleValveMode.Pressure,
            valve.Snapshot.CommandedMode);

        Assert.Equal(
            0,
            valve.Snapshot.PressureFeedback);

        await valve.ExecuteCycleAsync();

        Assert.Equal(
            ThrottleValveMode.Pressure,
            valve.Snapshot.ActualMode);

        // 0 + (4 - 0) * 0.5 = 2
        Assert.Equal(
            2,
            valve.Snapshot.PressureFeedback,
            precision: 6);
    }


    [Fact]
    public async Task ExecuteCycleAsync_RetriesPendingSafeModeAfterFeedbackRecovers()
    {
        var valve = CreateValve();

        await valve.InitializeAsync();
        await valve.ExecuteCycleAsync();

        await valve.SetPositionAsync(50);

        // 下一次安全写入失败。
        valve.FailNextWrites();

        // 位置反馈无效，基类应该撤销Position模式。
        valve.SetPositionFeedbackValid(false);

        await Assert.ThrowsAsync<IOException>(
            () => valve.ExecuteCycleAsync());

        // 安全命令写入失败，不能假装已经进入Closed。
        Assert.Equal(
            ThrottleValveMode.Position,
            valve.Snapshot.CommandedMode);

        // 模拟通信/反馈恢复。
        valve.SetPositionFeedbackValid(true);

        await valve.ExecuteCycleAsync();

        // 即使反馈已经恢复，上个周期遗留的安全请求
        // 仍然必须完成。
        Assert.Equal(
            ThrottleValveMode.Closed,
            valve.Snapshot.CommandedMode);
    }


}
