using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core;
using Kwy.Device.MotionCards.Googol;
using Kwy.Device.MotionCards.Leadshine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class MotionRuntimeRegistryTests
{
    [Fact]
    public void MultipleCards_AreResolvedByDeviceId()
    {
        var services = new ServiceCollection();
        services.AddKwyMotionServices();
        services.AddKwyGoogolMotionCard(config => ConfigureAxis(config, "Motion.Googol"));
        services.AddKwyLeadshineMotionCard(config => ConfigureAxis(config, "Motion.Leadshine"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionRuntimeRegistry registry = provider.GetRequiredService<IMotionRuntimeRegistry>();

        Assert.Equal(2, registry.Runtimes.Count);
        Assert.IsType<GoogolMotionCardDevice>(registry.GetRequired("Motion.Googol").Card);
        Assert.IsType<LeadshineMotionCardDevice>(registry.GetRequired("Motion.Leadshine").Card);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = provider.GetRequiredService<IAxisMotionExecutor>();
        });
    }

    [Fact]
    public void SingleCard_KeepsUnkeyedExecutorConvenience()
    {
        var services = new ServiceCollection();
        services.AddKwyMotionServices();
        services.AddKwyGoogolMotionCard(config => ConfigureAxis(config, "Motion.Main"));

        using ServiceProvider provider = services.BuildServiceProvider();
        IMotionRuntimeRegistry registry = provider.GetRequiredService<IMotionRuntimeRegistry>();

        Assert.Same(
            registry.GetRequired("Motion.Main").AxisExecutor,
            provider.GetRequiredService<IAxisMotionExecutor>());
    }

    private static void ConfigureAxis(GoogolMotionCardConfig config, string deviceId)
    {
        config.DeviceId = deviceId;
        config.Axes.Add(CreateAxis(deviceId));
    }

    private static void ConfigureAxis(LeadshineMotionCardConfig config, string deviceId)
    {
        config.DeviceId = deviceId;
        config.Axes.Add(CreateAxis(deviceId));
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
