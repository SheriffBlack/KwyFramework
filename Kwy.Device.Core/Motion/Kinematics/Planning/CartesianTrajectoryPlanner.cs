using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>以位置线性插值和四元数 SLERP 生成笛卡尔直线轨迹。</summary>
public sealed class CartesianTrajectoryPlanner : ICartesianTrajectoryPlanner
{
    public CartesianTrajectory PlanLinear(Pose6D start, Pose6D end, CartesianTrajectoryProfile profile)
    {
        start.Validate();
        end.Validate();
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        RigidPose from = start.ToRigidPose();
        RigidPose to = end.ToRigidPose();
        double distance = Distance(from, to);
        double angle = AngularDistanceDegrees(from.Orientation, to.Orientation);
        int segmentCount = Math.Max(1, Math.Max(
            (int)Math.Ceiling(distance / profile.MaximumLinearSegmentLength),
            (int)Math.Ceiling(angle / profile.MaximumAngularSegmentDegrees)));
        TimeSpan duration = TimeSpan.FromSeconds(Math.Max(distance / profile.LinearVelocity, angle / profile.AngularVelocity));
        var points = new CartesianTrajectoryPoint[segmentCount + 1];
        for (int index = 0; index <= segmentCount; index++)
        {
            double progress = index / (double)segmentCount;
            points[index] = new(progress, TimeSpan.FromTicks((long)(duration.Ticks * progress)), Interpolate(from, to, progress));
        }
        return new(start, end, profile, points);
    }

    private static RigidPose Interpolate(RigidPose from, RigidPose to, double progress)
        => new(
            from.X + (to.X - from.X) * progress,
            from.Y + (to.Y - from.Y) * progress,
            from.Z + (to.Z - from.Z) * progress,
            QuaternionD.Slerp(from.Orientation, to.Orientation, progress));

    private static double Distance(RigidPose left, RigidPose right)
        => Math.Sqrt(Math.Pow(right.X - left.X, 2) + Math.Pow(right.Y - left.Y, 2) + Math.Pow(right.Z - left.Z, 2));

    private static double AngularDistanceDegrees(QuaternionD left, QuaternionD right)
    {
        double dot = Math.Clamp(Math.Abs(QuaternionD.Dot(QuaternionD.Normalize(left), QuaternionD.Normalize(right))), -1d, 1d);
        return 2d * Math.Acos(dot) * 180d / Math.PI;
    }
}
