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

/// <summary>离线规划得到的关节轨迹点；全部使用业务轴 ID 和工程单位，脉冲留在物理卡适配器。</summary>
public sealed record JointTrajectoryPoint(
    TimeSpan TimeFromStart,
    IReadOnlyDictionary<string, double> Positions,
    IReadOnlyDictionary<string, double>? Velocities = null,
    IReadOnlyDictionary<string, double>? Accelerations = null);

/// <summary>
/// 离线规划、可达性和安全预检使用的关节轨迹。
/// 它不是通用实时执行命令；需要连续轮廓的控制器必须由厂商适配器编译为原生程序。
/// </summary>
public sealed record JointTrajectory(
    string MechanismId,
    long CoordinateFrameVersion,
    IReadOnlyList<JointTrajectoryPoint> Points)
{
    /// <summary>验证轨迹时间单调性与每个点的完整关节集合。</summary>
    public void Validate(IReadOnlyCollection<string> jointAxisIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(MechanismId);
        ArgumentNullException.ThrowIfNull(Points);
        ArgumentNullException.ThrowIfNull(jointAxisIds);
        if (CoordinateFrameVersion < 0) throw new ArgumentOutOfRangeException(nameof(CoordinateFrameVersion));
        if (Points.Count == 0) throw new ArgumentException("A joint trajectory requires at least one point.", nameof(Points));
        if (jointAxisIds.Count == 0 || jointAxisIds.Any(string.IsNullOrWhiteSpace) || jointAxisIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != jointAxisIds.Count)
            throw new ArgumentException("Joint axis IDs must be unique and non-empty.", nameof(jointAxisIds));
        TimeSpan previous = TimeSpan.MinValue;
        foreach (JointTrajectoryPoint point in Points)
        {
            if (point.TimeFromStart < TimeSpan.Zero || point.TimeFromStart <= previous)
                throw new ArgumentException("Joint trajectory timestamps must be non-negative and strictly increasing.", nameof(Points));
            previous = point.TimeFromStart;
            ValidateValues(point.Positions, jointAxisIds, nameof(point.Positions));
            if (point.Velocities is not null) ValidateValues(point.Velocities, jointAxisIds, nameof(point.Velocities));
            if (point.Accelerations is not null) ValidateValues(point.Accelerations, jointAxisIds, nameof(point.Accelerations));
        }
    }

    private static void ValidateValues(IReadOnlyDictionary<string, double> values, IReadOnlyCollection<string> axisIds, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != axisIds.Count || axisIds.Any(id => !values.TryGetValue(id, out double value) || !double.IsFinite(value)))
            throw new ArgumentException("Every joint axis requires one finite value.", parameterName);
    }
}

/// <summary>规划管线入口：将多段笛卡尔程序转换为已校验的关节轨迹，用于离线预检和厂商程序编译。</summary>
public interface IMotionPlanningPipeline
{
    Task<JointTrajectory> PlanAsync(MotionProgram program, CancellationToken cancellationToken = default);
}

/// <summary>完整关节轨迹的命令前安全校验；逐段检查，不能只校验最终目标点。</summary>
public interface IJointTrajectorySafetyValidator
{
    void Validate(JointTrajectory trajectory, MotionGroupDefinition group, IAxisDefinitionProvider axisDefinitions);
}
