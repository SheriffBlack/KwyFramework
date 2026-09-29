using Kwy.Device.Core;
using Kwy.Device.MotionCard.Core;
using Kwy.Device.MotionCard.Googol;
using Kwy.Device.MotionCard.Leadshine;
using Kwy.Device.MotionCard.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Configuration;
using Kwy.Device.Abstractions;

namespace Kwy.Device.MotionCard.Tests;

public sealed class MotionRuntimeRegistryTests
{
    [Fact]
    public void MultipleCards_AreResolvedByDeviceId()
    {
        var services = new ServiceCollection();
        services.AddMotionCardCore();
        services.AddAxisDefinitions([CreateAxis("Motion.Googol"), CreateAxis("Motion.Leadshine")]);
        services.AddGoogolMotionCard(config => ConfigureAxis(config, "Motion.Googol"));
        services.AddLeadshineMotionCard(config => ConfigureAxis(config, "Motion.Leadshine"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionRuntimeRegistry registry = provider.GetRequiredService<IMotionRuntimeRegistry>();
        IDeviceRegistry devices = provider.GetRequiredService<IDeviceRegistry>();

        Assert.Equal(2, registry.Runtimes.Count);
        Assert.IsType<GoogolMotionCardDevice>(registry.GetRequired("Motion.Googol").Card);
        Assert.IsType<LeadshineMotionCardDevice>(registry.GetRequired("Motion.Leadshine").Card);
        Assert.Equal(2, devices.Devices.Count);
        Assert.IsType<GoogolMotionCardDevice>(devices.GetRequiredDevice("Motion.Googol"));
        Assert.IsType<LeadshineMotionCardDevice>(devices.GetRequiredDevice("Motion.Leadshine"));
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = provider.GetRequiredService<IAxisMotionExecutor>();
        });
    }

    [Fact]
    public void SingleCard_KeepsUnkeyedExecutorConvenience()
    {
        var services = new ServiceCollection();
        services.AddMotionCardCore();
        services.AddAxisDefinitions([CreateAxis("Motion.Main")]);
        services.AddGoogolMotionCard(config => ConfigureAxis(config, "Motion.Main"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionRuntimeRegistry registry = provider.GetRequiredService<IMotionRuntimeRegistry>();

        Assert.Same(
            registry.GetRequired("Motion.Main").AxisExecutor,
            provider.GetRequiredService<IAxisMotionExecutor>());
    }

    [Fact]
    public void AutoModeGate_RejectsOfflineMotionController()
    {
        var services = new ServiceCollection();
        services.AddMotionCardCore();
        services.AddAxisDefinitions([CreateAxis("Motion.Simulation")]);
        services.AddSimulationMotionCard(config => config.DeviceId = "Motion.Simulation");
        services.AddMotionGroupDefinitions([]);

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionAutoModeGate gate = provider.GetRequiredService<IMotionAutoModeGate>();

        MotionConfigurationException exception = Assert.Throws<MotionConfigurationException>(gate.EnsureReadyForAutoMode);
        Assert.Contains(exception.Result.Issues, issue => issue.Code == "MotionControllerOffline");
    }

    [Fact]
    public async Task AutoModeGate_RejectsStoppedStateMonitor()
    {
        var services = new ServiceCollection();
        services.AddMotionCardCore();
        services.AddAxisDefinitions([CreateAxis("Motion.Simulation")]);
        services.AddSimulationMotionCard(config => config.DeviceId = "Motion.Simulation");
        services.AddMotionGroupDefinitions([]);

        using ServiceProvider provider = services.BuildServiceProvider();
        SimulationMotionCardDevice card = provider.GetRequiredService<SimulationMotionCardDevice>();
        await card.ConnectAsync();
        IMotionAutoModeGate gate = provider.GetRequiredService<IMotionAutoModeGate>();

        MotionConfigurationException exception = Assert.Throws<MotionConfigurationException>(gate.EnsureReadyForAutoMode);
        Assert.DoesNotContain(exception.Result.Issues, issue => issue.Code == "MotionControllerOffline");
        Assert.Contains(exception.Result.Issues, issue => issue.Code == "MotionStateMonitorStopped");
    }

    private static void ConfigureAxis(GoogolMotionCardConfig config, string deviceId)
    {
        config.DeviceId = deviceId;
    }

    private static void ConfigureAxis(LeadshineMotionCardConfig config, string deviceId)
    {
        config.DeviceId = deviceId;
    }

    private static AxisDefinition CreateAxis(string deviceId) => new()
    {
        Id = $"{deviceId}.axis.1",
        DisplayName = "Axis 1",
        DeviceId = deviceId,
        Channel = 1,
        Engineering = new AxisEngineeringConfig()
    };
}
