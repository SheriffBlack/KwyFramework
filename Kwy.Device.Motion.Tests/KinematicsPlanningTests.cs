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
}
