using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class KinematicsPlanningTests
{
    [Fact]
    public void QuaternionD_Slerp_ProducesUnitMidpointOrientation()
    {
        QuaternionD from = QuaternionD.Identity;
        QuaternionD to = QuaternionD.FromEulerDegrees(0, 0, 180);

        QuaternionD midpoint = QuaternionD.Slerp(from, to, 0.5);

        Assert.InRange(Math.Abs(QuaternionD.Dot(midpoint, midpoint) - 1d), 0d, 1e-12);
        Assert.InRange(Math.Abs(QuaternionD.Dot(midpoint, from)), 0.70d, 0.72d);
    }

    [Fact]
    public void CoordinateFrameRegistry_UpdatesOnlyDynamicFrameAndIncrementsVersion()
    {
        var frames = new CoordinateTransformService([
            new CoordinateFrameDefinition { Id = "machine" },
            new CoordinateFrameDefinition { Id = "workpiece", ParentFrameId = "machine", IsDynamic = true }
        ]);
        long version = frames.Version;

        frames.UpdateDynamicFrame("workpiece", new Pose6D(2, 0, 0, 0, 0, 0));
        Pose6D machinePose = frames.Transform(new Pose6D(1, 0, 0, 0, 0, 0), "workpiece", "machine");

        Assert.Equal(version + 1, frames.Version);
        Assert.Equal(3, machinePose.X, 10);
    }

    [Fact]
    public void CartesianTrajectoryPlanner_RespectsLinearSamplingConstraint()
    {
        var planner = new CartesianTrajectoryPlanner();
        CartesianTrajectory path = planner.PlanLinear(
            new Pose6D(0, 0, 0, 0, 0, 0),
            new Pose6D(1, 0, 0, 0, 0, 0),
            new CartesianTrajectoryProfile { LinearVelocity = 1, AngularVelocity = 10, MaximumLinearSegmentLength = 0.25, MaximumAngularSegmentDegrees = 1 });

        Assert.Equal(5, path.Points.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), path.Points[^1].TimeFromStart);
        Assert.Equal(1, path.Points[^1].Pose.X, 10);
    }

    [Fact]
    public void JointTrajectory_RejectsNonMonotonicTimeOrIncompleteJointSet()
    {
        var trajectory = new JointTrajectory("left-head", 1,
        [
            new(TimeSpan.Zero, new Dictionary<string, double> { ["left.j1"] = 0, ["left.j2"] = 0 }),
            new(TimeSpan.Zero, new Dictionary<string, double> { ["left.j1"] = 1, ["left.j2"] = 1 })
        ]);

        Assert.Throws<ArgumentException>(() => trajectory.Validate(["left.j1", "left.j2"]));
    }

    [Fact]
    public void JointTrajectorySafetyValidator_RejectsPathCrossingForbiddenZone()
    {
        var group = new MotionGroupDefinition
        {
            Id = "left.xy", DisplayName = "Left XY", DeviceId = "motion-1", AxisIds = ["left.x", "left.y"], CoordinateSystemChannel = 0,
            ForbiddenZones = [new MultiAxisForbiddenZone("fixture", new Dictionary<string, AxisForbiddenRange> { ["left.x"] = new(4, 6), ["left.y"] = new(4, 6) })]
        };
        var trajectory = new JointTrajectory("left-head", 1,
        [
            new(TimeSpan.Zero, new Dictionary<string, double> { ["left.x"] = 0, ["left.y"] = 0 }),
            new(TimeSpan.FromSeconds(1), new Dictionary<string, double> { ["left.x"] = 10, ["left.y"] = 10 })
        ]);

        MotionAdmissionDeniedException exception = Assert.Throws<MotionAdmissionDeniedException>(() => new JointTrajectorySafetyValidator().Validate(trajectory, group, new TestAxisDefinitions()));

        Assert.Equal("JointTrajectoryForbiddenPath", Assert.Single(exception.Violations).Code);
    }

    [Fact]
    public void JointTrajectoryTimeParameterizer_StretchesSegmentToAxisVelocityLimit()
    {
        var trajectory = new JointTrajectory("left-head", 1,
        [
            new(TimeSpan.Zero, new Dictionary<string, double> { ["left.x"] = 0, ["left.y"] = 0 }),
            new(TimeSpan.FromMilliseconds(1), new Dictionary<string, double> { ["left.x"] = 10, ["left.y"] = 0 })
        ]);
        var definitions = new TestAxisDefinitions(maximumVelocity: 2);

        JointTrajectory timed = new JointTrajectoryTimeParameterizer().Parameterize(trajectory, ["left.x", "left.y"], definitions);

        Assert.True(timed.Points[1].TimeFromStart >= TimeSpan.FromSeconds(5));
        Assert.InRange(timed.Points[1].Velocities!["left.x"], 0, 2);
    }

    [Fact]
    public void ControllerMotionProgramRequirements_RejectMissingNativeCapability()
    {
        var requirements = new ControllerMotionProgramRequirements
        {
            RequireNativeLookAhead = true,
            RequireNativeJerkLimiting = true
        };

        Assert.Throws<NotSupportedException>(() => requirements.ValidateAgainst(new MotionControllerCapabilities
        {
            SupportsNativeContinuousContour = true,
            SupportsNativeLookAhead = true
        }));
    }

    [Fact]
    public void CartesianVelocityLimiter_ScalesWholeCartesianVelocityForLimitingJoint()
    {
        var jacobian = new KinematicJacobian(
            ["left.x", "left.y"],
            [
                [2d, 0, 0, 0, 0, 0],
                [0d, 1, 0, 0, 0, 0]
            ]);

        CartesianVelocityLimitResult result = new CartesianVelocityLimiter().Limit(
            jacobian,
            new SpatialVelocity(new Vector3D(2, 0, 0), new Vector3D(0, 0, 0)),
            new TestAxisDefinitions(maximumVelocity: 2).Axes);

        Assert.Equal("left.x", result.LimitingAxisId);
        Assert.Equal(0.5, result.Scale, 10);
        Assert.Equal(2, result.LimitedJointVelocities["left.x"], 10);
    }

    private sealed class TestAxisDefinitions(double maximumVelocity = 100) : IAxisChannelDefinitionProvider
    {
        public IReadOnlyCollection<AxisDefinition> Axes { get; } =
        [Create("left.x", 1, maximumVelocity), Create("left.y", 2, maximumVelocity)];

        public AxisDefinition GetAxisDefinition(short axis) => Axes.Single(item => item.Channel == axis);

        private static AxisDefinition Create(string id, short channel, double maximumVelocity) => new()
        {
            Id = id, DisplayName = id, DeviceId = "motion-1", Channel = channel,
            Engineering = new AxisEngineeringConfig { Unit = MotionUnit.Millimeter, PulsesPerUnit = 1 },
            Limits = new AxisLimitConfig { MinimumPosition = -20, MaximumPosition = 20, MaximumVelocity = maximumVelocity, MaximumAcceleration = 100, MaximumDeceleration = 100 }
        };
    }
}
