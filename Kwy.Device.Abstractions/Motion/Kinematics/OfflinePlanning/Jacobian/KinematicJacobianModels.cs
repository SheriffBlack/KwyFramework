namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 内部笛卡尔速度：线速度使用位置工程单位/秒，角速度使用弧度/秒。
/// 业务层的 degree/秒必须在进入雅可比计算前转换，避免矩阵单位混用。
/// </summary>
public readonly record struct SpatialVelocity(Vector3D Linear, Vector3D AngularRadians);

/// <summary>机构当前位姿处的雅可比矩阵。每一行对应一个业务关节轴，六列依次为 X/Y/Z/Rx/Ry/Rz 速度分量。</summary>
public sealed record KinematicJacobian(
    IReadOnlyList<string> JointAxisIds,
    IReadOnlyList<IReadOnlyList<double>> Rows)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(JointAxisIds);
        ArgumentNullException.ThrowIfNull(Rows);
        if (JointAxisIds.Count == 0 || JointAxisIds.Any(string.IsNullOrWhiteSpace) || JointAxisIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != JointAxisIds.Count)
            throw new ArgumentException("Jacobian joint axis IDs must be unique and non-empty.", nameof(JointAxisIds));
        if (Rows.Count != JointAxisIds.Count || Rows.Any(row => row is null || row.Count != 6 || row.Any(value => !double.IsFinite(value))))
            throw new ArgumentException("Jacobian requires one finite six-element row per joint axis.", nameof(Rows));
    }

    public IReadOnlyDictionary<string, double> Map(SpatialVelocity velocity)
    {
        Validate();
        double[] twist = [velocity.Linear.X, velocity.Linear.Y, velocity.Linear.Z, velocity.AngularRadians.X, velocity.AngularRadians.Y, velocity.AngularRadians.Z];
        return JointAxisIds.Select((axisId, rowIndex) => new KeyValuePair<string, double>(axisId, Rows[rowIndex].Select((value, column) => value * twist[column]).Sum()))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>由具体机构提供当前姿态的雅可比矩阵；六轴、外部轴与双机构使用各自实现。</summary>
public interface IKinematicsJacobianProvider
{
    string MechanismId { get; }
    KinematicJacobian GetJacobian(RigidPose poseInBaseFrame, IReadOnlyDictionary<string, double> jointPositions);
}

/// <summary>离线雅可比速度分析结果；Scale 仅用于配方预检、预计时间和风险提示，不是控制器运行期的限速命令。</summary>
public sealed record CartesianVelocityLimitResult(
    double Scale,
    IReadOnlyDictionary<string, double> RequestedJointVelocities,
    IReadOnlyDictionary<string, double> LimitedJointVelocities,
    string? LimitingAxisId = null);

/// <summary>根据雅可比映射和轴速度上限进行离线速度可行性分析。</summary>
public interface ICartesianVelocityLimiter
{
    CartesianVelocityLimitResult Limit(
        KinematicJacobian jacobian,
        SpatialVelocity requestedVelocity,
        IReadOnlyCollection<AxisDefinition> axes);
}
