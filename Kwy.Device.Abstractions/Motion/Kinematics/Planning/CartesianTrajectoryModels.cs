namespace Kwy.Device.Abstractions.Motion;

/// <summary>笛卡尔空间轨迹的工程约束；线速度单位为位置工程单位/秒，角速度为度/秒。</summary>
public sealed record CartesianTrajectoryProfile
{
    public double LinearVelocity { get; init; } = 1;
    public double AngularVelocity { get; init; } = 10;
    /// <summary>离线预检中相邻位置点的最大距离，用于逆解连续性和路径安全分析，不定义控制器周期。</summary>
    public double MaximumLinearSegmentLength { get; init; } = 0.1;
    /// <summary>离线预检中相邻姿态点的最大夹角，避免逆解分析遗漏姿态变化。</summary>
    public double MaximumAngularSegmentDegrees { get; init; } = 1;

    public void Validate()
    {
        if (!double.IsFinite(LinearVelocity) || LinearVelocity <= 0) throw new ArgumentOutOfRangeException(nameof(LinearVelocity));
        if (!double.IsFinite(AngularVelocity) || AngularVelocity <= 0) throw new ArgumentOutOfRangeException(nameof(AngularVelocity));
        if (!double.IsFinite(MaximumLinearSegmentLength) || MaximumLinearSegmentLength <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumLinearSegmentLength));
        if (!double.IsFinite(MaximumAngularSegmentDegrees) || MaximumAngularSegmentDegrees <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumAngularSegmentDegrees));
    }
}

/// <summary>笛卡尔轨迹的一个内部采样点；位姿为四元数表示，避免插值过程出现欧拉角万向锁。</summary>
public readonly record struct CartesianTrajectoryPoint(double Progress, TimeSpan TimeFromStart, RigidPose Pose);

/// <summary>规划完成的空间轨迹。业务可追溯输入姿态，执行层只消费 Points。</summary>
public sealed record CartesianTrajectory(
    Pose6D StartPose,
    Pose6D EndPose,
    CartesianTrajectoryProfile Profile,
    IReadOnlyList<CartesianTrajectoryPoint> Points);

/// <summary>笛卡尔轨迹规划器；仅生成几何轨迹，不直接调用控制卡。</summary>
public interface ICartesianTrajectoryPlanner
{
    CartesianTrajectory PlanLinear(Pose6D start, Pose6D end, CartesianTrajectoryProfile profile);
}
