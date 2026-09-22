namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 工艺使用的六维空间位姿：位置单位遵循机械工程单位，Rx/Ry/Rz 为角度制。
/// 业务只传位姿和坐标系，不直接计算六个关节轴的位置。
/// </summary>
public readonly record struct Pose6D(double X, double Y, double Z, double Rx, double Ry, double Rz)
{
    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) || !double.IsFinite(Z)
            || !double.IsFinite(Rx) || !double.IsFinite(Ry) || !double.IsFinite(Rz))
            throw new ArgumentOutOfRangeException(nameof(Pose6D));
    }

    /// <summary>仅在空间层内部转换为四元数，避免业务配方承担万向锁和插值数学。</summary>
    public RigidPose ToRigidPose() => RigidPose.FromEulerDegrees(X, Y, Z, Rx, Ry, Rz);

    public static Pose6D FromRigidPose(RigidPose pose) => pose.ToEulerDegrees();
}

/// <summary>内部刚体位姿。姿态始终使用单位四元数；欧拉角仅作为业务输入和显示格式。</summary>
public readonly record struct RigidPose(double X, double Y, double Z, QuaternionD Orientation)
{
    public static RigidPose Identity => new(0, 0, 0, QuaternionD.Identity);
    public static RigidPose FromEulerDegrees(double x, double y, double z, double rx, double ry, double rz)
        => new(x, y, z, QuaternionD.FromEulerDegrees(rx, ry, rz));
    public static RigidPose Compose(RigidPose parent, RigidPose child)
    {
        Vector3D position = QuaternionD.Transform(new(child.X, child.Y, child.Z), parent.Orientation);
        return new(parent.X + position.X, parent.Y + position.Y, parent.Z + position.Z, QuaternionD.Normalize(parent.Orientation * child.Orientation));
    }
    public static RigidPose Inverse(RigidPose pose)
    {
        QuaternionD inverse = QuaternionD.Conjugate(QuaternionD.Normalize(pose.Orientation));
        Vector3D position = QuaternionD.Transform(new(-pose.X, -pose.Y, -pose.Z), inverse);
        return new(position.X, position.Y, position.Z, inverse);
    }
    public Pose6D ToEulerDegrees()
    {
        QuaternionD q = QuaternionD.Normalize(Orientation);
        double sinX = 2d * (q.W * q.X + q.Y * q.Z), cosX = 1d - 2d * (q.X * q.X + q.Y * q.Y);
        double sinY = Math.Clamp(2d * (q.W * q.Y - q.Z * q.X), -1d, 1d);
        double sinZ = 2d * (q.W * q.Z + q.X * q.Y), cosZ = 1d - 2d * (q.Y * q.Y + q.Z * q.Z);
        return new(X, Y, Z, ToDegrees(Math.Atan2(sinX, cosX)), ToDegrees(Math.Asin(sinY)), ToDegrees(Math.Atan2(sinZ, cosZ)));
    }
    private static double ToDegrees(double value) => value * 180d / Math.PI;
}

/// <summary>设备坐标系定义；TransformToParent 表示本坐标系原点在父坐标系中的位姿。</summary>
public sealed record CoordinateFrameDefinition
{
    /// <summary>稳定 ID，例如 machine、left-head-base、workpiece、camera。</summary>
    public required string Id { get; init; }
    /// <summary>父坐标系 ID；null 表示机械根坐标系。</summary>
    public string? ParentFrameId { get; init; }
    public Pose6D TransformToParent { get; init; }
    /// <summary>视觉标定、工件定位等允许运行时更新；机械根、机构基座等固定坐标系必须为 false。</summary>
    public bool IsDynamic { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        if (string.Equals(Id, ParentFrameId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A coordinate frame cannot be its own parent.");
        TransformToParent.Validate();
    }
}

/// <summary>一套运动学机构与其关节插补组的绑定，例如左/右六轴光学头。</summary>
public sealed record KinematicMechanismDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>逆解得到的关节轴由该插补组执行，成员顺序必须与求解器一致。</summary>
    public required string MotionGroupId { get; init; }
    /// <summary>该机构基座坐标系 ID。</summary>
    public required string BaseFrameId { get; init; }
    public required IReadOnlyList<string> JointAxisIds { get; init; }
    /// <summary>低于此奇异度的逆解会被拒绝；具体指标的量纲由机构求解器定义。</summary>
    public double MinimumSingularityMetric { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(MotionGroupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseFrameId);
        ArgumentNullException.ThrowIfNull(JointAxisIds);
        if (JointAxisIds.Count < 2 || JointAxisIds.Any(string.IsNullOrWhiteSpace)
            || JointAxisIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != JointAxisIds.Count)
            throw new ArgumentException("A kinematic mechanism requires unique joint axis IDs.", nameof(JointAxisIds));
        if (!double.IsFinite(MinimumSingularityMetric) || MinimumSingularityMetric < 0) throw new ArgumentOutOfRangeException(nameof(MinimumSingularityMetric));
    }
}

/// <summary>一组逆解候选。求解器不直接选择最终解，Core 会结合当前关节位置和轴限位择优。</summary>
public sealed record KinematicSolution(
    IReadOnlyDictionary<string, double> JointPositions,
    bool IsReachable,
    double? SingularityMetric = null,
    string? Diagnostic = null);

/// <summary>将空间位姿转换为关节工程位置的机构专属求解器；六轴几何参数由具体设备实现提供。</summary>
public interface IKinematicsSolver
{
    string MechanismId { get; }
    IReadOnlyList<KinematicSolution> SolveInverseCandidates(RigidPose poseInBaseFrame);
    RigidPose SolveForward(IReadOnlyDictionary<string, double> jointPositions);
}

/// <summary>坐标系链查询服务，负责坐标系转换与环路校验。</summary>
public interface ICoordinateTransformService
{
    Pose6D Transform(Pose6D pose, string sourceFrameId, string targetFrameId);
}

/// <summary>坐标系注册表。更新动态坐标系会产生新版本；已开始的动作应绑定其启动时快照。</summary>
public interface ICoordinateFrameRegistry : ICoordinateTransformService
{
    long Version { get; }
    void UpdateDynamicFrame(string frameId, Pose6D transformToParent);
}

/// <summary>工艺空间运动入口。当前输出关节空间同步定位；连续笛卡尔轨迹应由后续轨迹规划器分段并前瞻执行。</summary>
public interface IPoseMotionExecutor
{
    Task MoveToPoseAsync(
        string mechanismId,
        string sourceFrameId,
        Pose6D targetPose,
        MotionProfile? profile = null,
        double? tolerance = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
