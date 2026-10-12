using KwyPecvd.Device;
using KwyPecvd.Device.RfPowerSupply;
using KwyPecvd.Device.RfPowerSupply.Simulated;

namespace KwyPecvd.Tests.Devices;

public sealed class SimulatedRfPowerSupplyTests
{
    [Fact]
    public async Task EnableAsync_ProducesForwardPower()
    {
        var rf = CreateRfPowerSupply(
            responseFactor: 1);

        await rf.InitializeAsync();

        // 首次采样后，设备才从初始“未采样”状态
        // 变成在线且反馈有效。
        await rf.ExecuteCycleAsync();

        var setResult =
            await rf.SetPowerAsync(500);

        var enableResult =
            await rf.EnableAsync();

        await rf.ExecuteCycleAsync();

        Assert.True(setResult.Succeeded);
        Assert.True(enableResult.Succeeded);

        Assert.Equal(
            500,
            rf.Snapshot.SetPoint);

        Assert.Equal(
            500,
            rf.Snapshot.ForwardPower);

        Assert.True(
            rf.Snapshot.CommandedEnabled);

        Assert.True(
            rf.Snapshot.IsOutputOn);

        Assert.False(
            rf.Snapshot.IsOffline);

        Assert.True(
            rf.Snapshot.IsFeedbackValid);
    }

    [Fact]
    public async Task ReflectedPowerHigh_RevokesOutputCommand()
    {
        var rf = CreateRfPowerSupply(
            responseFactor: 1,
            reflectedPowerRatio: 0.5,
            reflectedPowerLimit: 100,
            reflectedPowerDelay:
                TimeSpan.Zero);

        await rf.InitializeAsync();
        await rf.ExecuteCycleAsync();

        await rf.SetPowerAsync(500);
        await rf.EnableAsync();

        // 本周期硬件先报告输出开启和反射功率过高，
        // 基类随后撤销软件输出请求。
        await rf.ExecuteCycleAsync();

        Assert.Equal(
            250,
            rf.Snapshot.ReflectedPower);

        Assert.True(
            rf.Snapshot.IsReflectedPowerHigh);

        Assert.False(
            rf.Snapshot.CommandedEnabled);

        Assert.Equal(
            RfPowerSupplyDiagnosticCodes
                .ReflectedPowerHigh,
            rf.Snapshot.DiagnosticCode);

        // 下一周期才真正把关闭命令发送给设备。
        await rf.ExecuteCycleAsync();

        Assert.False(
            rf.Snapshot.IsOutputOn);
    }

    [Fact]
    public async Task EnableAsync_RejectsWhenOffline()
    {
        var rf = CreateRfPowerSupply();

        rf.SetOffline(true);

        await rf.InitializeAsync();
        await rf.ExecuteCycleAsync();
        await rf.SetPowerAsync(500);

        var result =
            await rf.EnableAsync();

        Assert.False(result.Succeeded);

        Assert.Equal(
            ResultCodes.Offline,
            result.Code);

        Assert.False(
            rf.Snapshot.CommandedEnabled);

        Assert.False(
            rf.Snapshot.IsOutputOn);
    }

    [Fact]
    public async Task GeneratorFault_RevokesOutputCommand()
    {
        var rf = CreateRfPowerSupply(
            responseFactor: 1);

        await rf.InitializeAsync();
        await rf.ExecuteCycleAsync();

        await rf.SetPowerAsync(500);
        await rf.EnableAsync();
        await rf.ExecuteCycleAsync();

        Assert.True(
            rf.Snapshot.IsOutputOn);

        rf.SetFaulted(true);

        await rf.ExecuteCycleAsync();

        Assert.True(
            rf.Snapshot.IsFaulted);

        Assert.False(
            rf.Snapshot.CommandedEnabled);

        Assert.False(
            rf.Snapshot.IsOutputOn);

        Assert.Equal(
            RfPowerSupplyDiagnosticCodes
                .GeneratorFault,
            rf.Snapshot.DiagnosticCode);
    }

    [Fact]
    public async Task ForwardPowerDeviation_RaisesOutOfTolerance()
    {
        var rf = CreateRfPowerSupply(
            responseFactor: 0.2,
            tolerance: 10,
            toleranceDelay: TimeSpan.Zero);

        await rf.InitializeAsync();
        await rf.ExecuteCycleAsync();

        await rf.SetPowerAsync(500);
        await rf.EnableAsync();
        await rf.ExecuteCycleAsync();

        Assert.Equal(
            500,
            rf.Snapshot.SetPoint);

        Assert.Equal(
            100,
            rf.Snapshot.ForwardPower);

        Assert.True(
            rf.Snapshot.IsOutOfTolerance);

        Assert.True(
            rf.Snapshot.CommandedEnabled);

        Assert.True(
            rf.Snapshot.IsOutputOn);

        Assert.Equal(
            RfPowerSupplyDiagnosticCodes
                .ForwardPowerOutOfTolerance,
            rf.Snapshot.DiagnosticCode);
    }

    private static SimulatedRfPowerSupply CreateRfPowerSupply(
        double responseFactor = 0.2,
        double reflectedPowerRatio = 0.02,
        double reflectedPowerLimit = 100,
        TimeSpan? reflectedPowerDelay = null,
        double tolerance = 10,
        TimeSpan? toleranceDelay = null)
    {
        var definition =
            new RfPowerSupplyDefinition
            {
                Id = "RF_PM1",
                ModuleId = "PM1",
                DisplayName =
                    "PM1 RF Power Supply",
                Unit = "W",
                FrequencyHz = 13_560_000,
                MaximumPower = 1000,
                ReflectedPowerLimit =
                    reflectedPowerLimit,
                Tolerance = tolerance,
                ReflectedPowerDelay =
                    reflectedPowerDelay ??
                    TimeSpan.FromSeconds(1),
                ToleranceDelay =
                    toleranceDelay ??
                    TimeSpan.FromSeconds(3)
            };

        return new SimulatedRfPowerSupply(
            definition,
            responseFactor,
            reflectedPowerRatio);
    }
}