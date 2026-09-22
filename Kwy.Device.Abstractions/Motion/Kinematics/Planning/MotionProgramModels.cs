namespace Kwy.Device.Abstractions.Motion;

/// <summary>连续轮廓运动的一个语义段。业务提交段，不提交控制器周期点或物理轴号。</summary>
public abstract record CartesianTrajectorySegment(string Id)
{
    public abstract Pose6D EndPose { get; }
}

/// <summary>从上一段终点直线运动到 EndPose。</summary>
public sealed record CartesianLineSegment(string Id, Pose6D TargetPose) : CartesianTrajectorySegment(Id)
{
    public override Pose6D EndPose => TargetPose;
}

/// <summary>圆弧段。圆心与终点位姿均在同一业务坐标系中；姿态插补由轨迹规划器负责。</summary>
public sealed record CartesianArcSegment(string Id, Pose6D TargetPose, Vector3D Center, ArcDirection Direction) : CartesianTrajectorySegment(Id)
{
    public override Pose6D EndPose => TargetPose;
}

/// <summary>多段连续轮廓程序，可被前瞻规划器整体读取、限速和圆角处理。</summary>
public sealed record MotionProgram(
    string Id,
    string MechanismId,
    string SourceFrameId,
    Pose6D StartPose,
    IReadOnlyList<CartesianTrajectorySegment> Segments,
    CartesianTrajectoryProfile Profile)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(MechanismId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceFrameId);
        StartPose.Validate();
        ArgumentNullException.ThrowIfNull(Segments);
        ArgumentNullException.ThrowIfNull(Profile);
        if (Segments.Count == 0 || Segments.Any(item => string.IsNullOrWhiteSpace(item.Id))) throw new ArgumentException("A motion program requires named segments.", nameof(Segments));
        if (Segments.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Segments.Count) throw new ArgumentException("Motion program segment IDs must be unique.", nameof(Segments));
        foreach (CartesianTrajectorySegment segment in Segments) segment.EndPose.Validate();
        Profile.Validate();
    }
}

/// <summary>时间参数化后的关节轨迹点；全部使用业务轴 ID 和工程单位，脉冲留在物理卡适配器。</summary>
public sealed record JointTrajectoryPoint(
    TimeSpan TimeFromStart,
    IReadOnlyDictionary<string, double> Positions,
    IReadOnlyDictionary<string, double>? Velocities = null,
    IReadOnlyDictionary<string, double>? Accelerations = null);

/// <summary>可交给控制器连续执行能力的关节轨迹；生成后不可随坐标系动态更新而改变。</summary>
public sealed record JointTrajectory(
    string MechanismId,
    long CoordinateFrameVersion,
    IReadOnlyList<JointTrajectoryPoint> Points);

/// <summary>规划管线入口：将多段笛卡尔程序转换为已校验、已时间参数化的关节轨迹。</summary>
public interface IMotionPlanningPipeline
{
    Task<JointTrajectory> PlanAsync(MotionProgram program, CancellationToken cancellationToken = default);
}