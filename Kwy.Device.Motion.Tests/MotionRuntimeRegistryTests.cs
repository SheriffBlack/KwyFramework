using Kwy.Device.Core;
using Kwy.Device.Motion.Core;
using Kwy.Device.Motion.Googol;
using Kwy.Device.Motion.Leadshine.Dmc;
using Kwy.Device.Motion.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Configuration;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Motion.Tests;

public sealed class MotionRuntimeRegistryTests
{
    [Fact]
    public void MultipleCards_AreResolvedByDeviceId()
    {
        var services = new ServiceCollection();
        services.AddMotionCore();
        services.AddAxisDefinitions([CreateAxis("Motion.Googol"), CreateAxis("Motion.Leadshine")]);
        services.AddGoogolMotionCard(config => ConfigureAxis(config, "Motion.Googol"));
        services.AddLeadshineDmc3800(config => ConfigureAxis(config, "Motion.Leadshine"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionRuntimeRegistry registry = provider.GetRequiredService<IMotionRuntimeRegistry>();
        IDeviceRegistry devices = provider.GetRequiredService<IDeviceRegistry>();

        Assert.Equal(2, registry.Runtimes.Count);
        Assert.IsType<GoogolMotionCardDevice>(registry.GetRequired("Motion.Googol").Controller);
        Assert.IsType<Dmc3800Device>(registry.GetRequired("Motion.Leadshine").Controller);
        Assert.Equal(2, devices.Devices.Count);
        Assert.IsType<GoogolMotionCardDevice>(devices.GetRequiredDevice("Motion.Googol"));
        Assert.IsType<Dmc3800Device>(devices.GetRequiredDevice("Motion.Leadshine"));
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = provider.GetRequiredService<IAxisMotionExecutor>();
        });
    }

    [Fact]
    public void SingleCard_KeepsUnkeyedExecutorConvenience()
    {
        var services = new ServiceCollection();
        services.AddMotionCore();
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
        services.AddMotionCore();
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
        services.AddMotionCore();
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

    private static void ConfigureAxis(Dmc3800Config config, string deviceId)
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
