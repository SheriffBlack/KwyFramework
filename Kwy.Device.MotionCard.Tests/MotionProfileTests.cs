using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Googol;
using Kwy.Device.MotionCard.Leadshine;
using Xunit;

namespace Kwy.Device.MotionCard.Tests;

/// <summary>验证业务速度曲线语义与厂商坐标系原生参数的边界。</summary>
public sealed class MotionProfileTests
{
    [Fact]
    public void RequireNativeSCurve_IsPreservedByMotionProfile()
    {
        var profile = new MotionProfile(100, 500, 400, MotionSmoothingMode.RequireNativeSCurve);

        Assert.Equal(MotionSmoothingMode.RequireNativeSCurve, profile.Smoothing);
        Assert.Equal(400, profile.Deceleration);
    }

    [Fact]
    public void LeadshineCoordinateSystem_RejectsInvalidNativeSProfile()
    {
        var coordinate = new LeadshineCoordinateSystemConfig
        {
            CoordinateSystem = 1,
            Axes = [1, 2],
            SProfile = new LeadshineVectorSProfileOptions { Mode = 0, Parameter = -1 }
        };

        Assert.False(coordinate.Validate(axisCount: 4, maximumCoordinateSystem: 2));
    }

    [Fact]
    public void GoogolCoordinateSystem_UsesExplicitNativeSynchronousLimits()
    {
        var coordinate = new GoogolCoordinateSystemConfig
        {
            CoordinateSystem = 1,
            Axes = [1, 2],
            SynchronousVelocityLimit = 200,
            SynchronousAccelerationLimit = 800,
            CornerSmoothingTime = 20
        };

        Assert.True(coordinate.Validate(axisCount: 4, maximumCoordinateSystem: 2));
    }
}
