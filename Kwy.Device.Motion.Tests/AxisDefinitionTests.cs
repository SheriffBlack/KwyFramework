using Kwy.Device.Abstractions.Motion;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class AxisDefinitionTests
{
    [Fact]
    public void Validate_AcceptsCompleteLinearAxisDefinition()
    {
        AxisDefinition definition = CreateDefinition();

        definition.Validate();

        Assert.Equal(new AxisAddress("motion-card-1", 1), definition.PhysicalId);
        Assert.Equal(0.005, definition.Defaults.ToExecutionOptions().PositionTolerance);
    }

    [Fact]
    public void EngineeringConfig_DoesNotContainPhysicalAxisIdentity()
    {
        AxisDefinition definition = CreateDefinition();

        definition.Validate();
        Assert.Equal((short)1, definition.Channel);
        Assert.Equal(new AxisAddress("motion-card-1", 1), definition.PhysicalId);
    }

    [Fact]
    public void Validate_RejectsRotarySettingsOnLinearUnit()
    {
        AxisDefinition definition = CreateDefinition() with { Rotary = new RotaryAxisDefinition() };

        Assert.Throws<InvalidOperationException>(definition.Validate);
    }

    [Fact]
    public void Validate_AcceptsDegreeAxisWithCableTwistLimits()
    {
        AxisDefinition definition = CreateDefinition() with
        {
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Degree, PulsesPerUnit = 1_000 },
            Rotary = new RotaryAxisDefinition
            {
                PositionMode = RotaryPositionMode.Accumulated,
                MinimumAccumulatedAngle = -720,
                MaximumAccumulatedAngle = 720
            }
        };

        definition.Validate();
    }

    private static AxisDefinition CreateDefinition() => new()
    {
        Id = "transport.x",
        DisplayName = "Transport X",
        DeviceId = "motion-card-1",
        Channel = 1,
        Engineering = new AxisEngineeringConfig
        {
            Unit = MotionUnit.Millimeter,
            PulsesPerUnit = 10_000
        },
        Limits = new AxisLimitConfig
        {
            MinimumPosition = 0,
            MaximumPosition = 300,
            MaximumVelocity = 200,
            MaximumAcceleration = 1_000,
            MaximumDeceleration = 1_000
        },
        Defaults = new AxisMotionDefaults { PositionTolerance = 0.005 }
    };
}
