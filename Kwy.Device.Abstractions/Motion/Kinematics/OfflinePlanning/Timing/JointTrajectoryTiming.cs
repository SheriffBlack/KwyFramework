namespace Kwy.Device.Abstractions.Motion;

/// <summary>关节轨迹时间参数化的速度缩放。取值范围 (0, 1]，用于工艺降速而不突破轴级硬上限。</summary>
public sealed record JointTrajectoryTimingOptions
{
    public double VelocityScale { get; init; } = 1;
    public double AccelerationScale { get; init; } = 1;

    public void Validate()
    {
        if (!double.IsFinite(VelocityScale) || VelocityScale <= 0 || VelocityScale > 1) throw new ArgumentOutOfRangeException(nameof(VelocityScale));
        if (!double.IsFinite(AccelerationScale) || AccelerationScale <= 0 || AccelerationScale > 1) throw new ArgumentOutOfRangeException(nameof(AccelerationScale));
    }
}

/// <summary>
/// 将几何关节路径转换为满足轴速度、加速度、减速度上限的离线时间估算轨迹。
/// 仅用于仿真、配方预检和预计时间；Jerk 平滑与实时插补必须由控制器原生能力负责。
/// </summary>
public interface IJointTrajectoryTimeParameterizer
{
    JointTrajectory Parameterize(
        JointTrajectory geometricTrajectory,
        IReadOnlyCollection<string> jointAxisIds,
        IAxisChannelDefinitionProvider axisDefinitions,
        JointTrajectoryTimingOptions? options = null);
}
