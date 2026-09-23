using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class AxisAdvancedServicesTests
{
    [Fact]
    public void CoordinateTransformer_AppliesZeroOffsetsAndErrorCompensation()
    {
        AxisDefinition axis = CreateAxis() with
        {
            Calibration = new AxisCalibrationConfig
            {
                MechanicalZeroOffset = 10,
                CalibrationZeroOffset = -2,
                ErrorCompensationTableId = "x-table"
            }
        };
        var transformer = new AxisCoordinateTransformer(new ConstantCompensationProvider(0.25));

        Assert.Equal(108.25, transformer.ToMachinePosition(axis, 100), 6);
    }

    [Fact]
    public void RotaryPlanner_ChoosesShortestLegalModuloTarget()
    {
        AxisDefinition axis = CreateAxis() with
        {
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Degree, PulsesPerUnit = 1_000 },
            Rotary = new RotaryAxisDefinition
            {
                PositionMode = RotaryPositionMode.Modulo,
                Period = 360,
                MinimumAccumulatedAngle = -720,
                MaximumAccumulatedAngle = 720
            }
        };

        double target = new RotaryAxisPathPlanner().ResolveTarget(axis, currentPosition: 350, requestedPosition: 10);

        Assert.Equal(370, target);
    }

    [Fact]
    public void RotaryPlanner_ResolvesModuloTargetFarFromMechanicalZero()
    {
        AxisDefinition axis = CreateAxis() with
        {
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Degree },
            Rotary = new RotaryAxisDefinition
            {
                PositionMode = RotaryPositionMode.Modulo,
                Period = 360,
                PreferredDirection = 1,
                MaximumAccumulatedAngle = 20_000
            }
        };

        double target = new RotaryAxisPathPlanner().ResolveTarget(axis, currentPosition: 10_000, requestedPosition: 10);

        Assert.Equal(10_090, target);
    }

    [Fact]
    public void MultiAxisGuard_RejectsTargetInsideForbiddenZone()
    {
        var guard = new MultiAxisSafetyGuard([
            new MultiAxisForbiddenZone("camera", new Dictionary<string, AxisForbiddenRange>
            {
                ["stage.x"] = new(10, 20),
                ["stage.y"] = new(30, 40)
            })
        ]);

        MotionAdmissionResult result = guard.Validate(new Dictionary<string, double> { ["stage.x"] = 15, ["stage.y"] = 35 });

        Assert.False(result.IsAllowed);
        Assert.Equal("ForbiddenZone", Assert.Single(result.Violations).Code);
    }

    [Fact]
    public void MultiAxisGuard_RejectsEmptyZoneDefinition()
    {
        Assert.Throws<ArgumentException>(() => new MultiAxisSafetyGuard([
            new MultiAxisForbiddenZone("empty", new Dictionary<string, AxisForbiddenRange>())
        ]));
    }

    private static AxisDefinition CreateAxis() => new()
    {
        Id = "stage.x",
        DisplayName = "Stage X",
        DeviceId = "motion-1",
        Channel = 1,
        Engineering = new AxisEngineeringConfig()
    };

    private sealed class ConstantCompensationProvider(double value) : IAxisErrorCompensationProvider
    {
        public double GetCompensation(string tableId, double engineeringPosition) => value;
    }
}
