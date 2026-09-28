using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Core;
using Kwy.Device.MotionCards.Simulation;
using Kwy.Device.MotionCards.Googol;
using Kwy.Device.MotionCards.Leadshine;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class MotionBehaviorTests
{
    [Fact]
    public void EngineeringConverter_PreservesNativePulseUnits()
    {
        var config = new AxisEngineeringConfig { Unit = MotionUnit.Pulse };

        Assert.Equal(100, AxisEngineeringConverter.ToNativePosition(100, config));
        Assert.Equal(20, AxisEngineeringConverter.ToNativeVelocity(20, config));
        Assert.Equal(0.5, AxisEngineeringConverter.ToNativeAcceleration(0.5, config));
    }

    [Fact]
    public void EngineeringConverter_ConvertsMillimeterUnits()
    {
        var config = new AxisEngineeringConfig
        {
            Unit = MotionUnit.Millimeter,
            PulsesPerUnit = 10_000
        };

        Assert.Equal(100_000, AxisEngineeringConverter.ToNativePosition(10, config));
        Assert.Equal(500, AxisEngineeringConverter.ToNativeVelocity(50, config));
        Assert.Equal(5, AxisEngineeringConverter.ToNativeAcceleration(500, config));
    }

    [Fact]
    public void AxisDefinitionProvider_IndexesEngineeringSettingsByPhysicalChannel()
    {
        AxisDefinition axis = CreateAxisDefinition("SimulationMotion", 2) with
        {
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Millimeter, PulsesPerUnit = 2_000 }
        };

        var definitions = new AxisDefinitionProvider([axis]);
        Assert.True(definitions.TryGet(new AxisAddress("SimulationMotion", 2), out AxisDefinition found));
        Assert.Equal(2_000, found.Engineering.PulsesPerUnit);
    }

    [Fact]
    public void AxisDefinitionProvider_RejectsDuplicatePhysicalChannels()
    {
        Assert.Throws<ArgumentException>(() => new AxisDefinitionProvider([
            CreateAxisDefinition("SimulationMotion", 1),
            CreateAxisDefinition("SimulationMotion", 1) with { Id = "SimulationMotion.axis.other" }]));
    }

    [Fact]
    public async Task SimulationCard_MovesAndHomes()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);

        card.MoveAbs(1, 25, new MotionProfile(100, 1_000, 1_000));
        MotionCompletionResult completion = await card.WaitForAxisCompletedAsync(
            1,
            targetPosition: 25,
            tolerance: 0.001,
            timeout: TimeSpan.FromSeconds(2));
        Assert.Equal(25, card.GetPosition(1), 6);
        Assert.Equal(25, completion.ActualPosition, 6);

        card.GoHome(1);
        HomeStatus status = await card.WaitForHomeCompletedAsync(1, TimeSpan.FromSeconds(2));
        Assert.Equal(HomeState.Succeeded, status.State);
        Assert.Equal(0, card.GetPosition(1), 6);
    }

    [Fact]
    public async Task SimulationCard_CompletionWaitReportsLimitReachedBeforeTarget()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);

        card.MoveAbs(1, 100, new MotionProfile(10, 100, 100));
        await Task.Delay(20);
        card.SetLimit(1, positive: true, negative: false);

        MotionLimitException exception = await Assert.ThrowsAsync<MotionLimitException>(() =>
            card.WaitForAxisCompletedAsync(1, 100, 0.001, TimeSpan.FromSeconds(2)));
        Assert.True(exception.IsPositiveLimit);
    }

    [Fact]
    public async Task SimulationCard_CompletionWaitRejectsStoppedShortOfTarget()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);

        card.MoveAbs(1, 100, new MotionProfile(10, 100, 100));
        await Task.Delay(20);
        card.Stop(1);

        await Assert.ThrowsAsync<MotionPositionException>(() =>
            card.WaitForAxisCompletedAsync(1, 100, 0.001, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task SimulationCard_CompletionWaitReportsAlarmDuringMotion()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);

        card.MoveAbs(1, 100, new MotionProfile(10, 100, 100));
        await Task.Delay(20);
        card.SetAlarm(1, true);

        await Assert.ThrowsAsync<MotionAlarmException>(() =>
            card.WaitForAxisCompletedAsync(1, 100, 0.001, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task AxisSnapshot_MapsConfirmedControllerFaults()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);

        card.SetAlarm(1, true);
        MotionAxisSnapshot alarm = card.GetAxisSnapshot(1);
        Assert.Equal(AxisFaultCode.ControllerAlarm, alarm.Fault?.Code);
        Assert.Equal(AxisFaultSeverity.StopRequired, alarm.Fault?.Severity);

        card.SetAlarm(1, false);
        card.SetLimit(1, positive: true, negative: false);
        MotionAxisSnapshot limit = card.GetAxisSnapshot(1);
        Assert.Equal(AxisFaultCode.PositiveLimitReached, limit.Fault?.Code);
    }

    [Fact]
    public async Task SimulationCard_RejectsMotionWhileAlarmIsInjected()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);
        card.SetAlarm(1, true);

        Assert.Throws<InvalidOperationException>(() =>
            card.MoveAbs(1, 10, new MotionProfile(10, 100, 100)));
    }

    [Fact]
    public async Task SimulationCard_EnforcesSoftwareLimits()
    {
        await using var card = CreateCard();
        await card.ConnectAsync();
        card.ServoOn(1);
        card.SetSoftLimit(1, positive: 10, negative: -10);

        Assert.Throws<InvalidOperationException>(() =>
            card.MoveAbs(1, 11, new MotionProfile(10, 100, 100)));
    }

    [Fact]
    public async Task InMemoryNamedPositionRepository_ReplacesNamesCaseInsensitively()
    {
        var repository = new InMemoryNamedPositionRepository();
        await repository.SaveAsync(new NamedPositionSet("Load", new Dictionary<string, double> { ["stage.x"] = 10 }));
        await repository.SaveAsync(new NamedPositionSet("load", new Dictionary<string, double> { ["stage.x"] = 20 }));

        IReadOnlyList<NamedPositionSet> all = await repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(20, (await repository.GetAsync("LOAD"))!.Positions["stage.x"]);
    }

    [Fact]
    public void GoogolConfig_AcceptsHardwareCoordinateChannels()
    {
        var config = new GoogolMotionCardConfig { AxisCount = 2 };
        config.CoordinateSystems.Add(new GoogolCoordinateSystemConfig
        {
            CoordinateSystem = 1,
            Axes = new short[] { 1, 2 },
            MaximumVelocity = 100,
            MaximumAcceleration = 500
        });

        Assert.True(config.Validate());
    }

    [Fact]
    public void GoogolConfig_DoesNotOwnEngineeringScaleDefinitions()
    {
        var config = new GoogolMotionCardConfig { AxisCount = 2 };
        config.CoordinateSystems.Add(new GoogolCoordinateSystemConfig
        {
            CoordinateSystem = 1,
            Axes = new short[] { 1, 2 }
        });

        Assert.True(config.Validate());
    }

    [Fact]
    public void LeadshineConfig_ContainsOnlyVendorAxisOptions()
    {
        var config = new LeadshineMotionCardConfig();
        config.AxisOptions[1] = new LeadshineAxisOptions { HomeMode = 3, EzCount = 1 };

        Assert.True(config.Validate());
        Assert.Equal((ushort)3, config.GetAxisOptions(1).HomeMode);
    }

    [Fact]
    public void LeadshineConfig_AcceptsVendorOptionsForPhysicalAxis()
    {
        var config = new LeadshineMotionCardConfig();
        config.AxisOptions[1] = new LeadshineAxisOptions();

        Assert.True(config.Validate());
    }

    private static SimulationMotionCardDevice CreateCard()
    {
        var config = new SimulationMotionCardConfig
        {
            AxisCount = 1,
            UpdateInterval = TimeSpan.FromMilliseconds(5),
            SimulationSpeedRatio = 10
        };
        return new(config, new AxisDefinitionProvider([CreateAxisDefinition(config.DeviceId, 1)]));
    }

    private static AxisDefinition CreateGoogolAxis(short axis)
        => CreateAxisDefinition("Googol-0", axis) with
        {
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Millimeter, PulsesPerUnit = 10_000 },
            Limits = new AxisLimitConfig
            {
                MinimumPosition = 0,
                MaximumPosition = 300,
                MaximumVelocity = 200,
                MaximumAcceleration = 1_000,
                MaximumDeceleration = 1_000
            }
        };

    private static AxisDefinition CreateAxisDefinition(string deviceId, short axis) => new()
    {
        Id = $"{deviceId}.axis.{axis}",
        DisplayName = $"Axis {axis}",
        DeviceId = deviceId,
        Channel = axis,
        Engineering = new AxisEngineeringConfig()
    };
}
