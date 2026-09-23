using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>基于各轴速度/加减速度上限的保守离线时间估算器，不参与控制器实时点流。</summary>
public sealed class JointTrajectoryTimeParameterizer : IJointTrajectoryTimeParameterizer
{
    public JointTrajectory Parameterize(JointTrajectory geometricTrajectory, IReadOnlyCollection<string> jointAxisIds, IAxisDefinitionProvider axisDefinitions, JointTrajectoryTimingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(geometricTrajectory);
        ArgumentNullException.ThrowIfNull(jointAxisIds);
        ArgumentNullException.ThrowIfNull(axisDefinitions);
        options ??= new JointTrajectoryTimingOptions();
        options.Validate();
        geometricTrajectory.Validate(jointAxisIds);
        AxisDefinition[] axes = jointAxisIds.Select(axisId => axisDefinitions.Axes.SingleOrDefault(axis => string.Equals(axis.Id, axisId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Axis '{axisId}' is not defined on the motion card.")).ToArray();

        var result = new List<JointTrajectoryPoint>(geometricTrajectory.Points.Count);
        JointTrajectoryPoint first = geometricTrajectory.Points[0];
        result.Add(new(TimeSpan.Zero, Copy(first.Positions), Zeroes(jointAxisIds)));
        TimeSpan elapsed = TimeSpan.Zero;
        for (int index = 1; index < geometricTrajectory.Points.Count; index++)
        {
            JointTrajectoryPoint previous = geometricTrajectory.Points[index - 1];
            JointTrajectoryPoint current = geometricTrajectory.Points[index];
            TimeSpan duration = RequiredDuration(previous.Positions, current.Positions, axes, options);
            elapsed += duration;
            var velocities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (AxisDefinition axis in axes)
                velocities[axis.Id] = (current.Positions[axis.Id] - previous.Positions[axis.Id]) / duration.TotalSeconds;
            result.Add(new(elapsed, Copy(current.Positions), velocities));
        }
        var timed = new JointTrajectory(geometricTrajectory.MechanismId, geometricTrajectory.CoordinateFrameVersion, result);
        timed.Validate(jointAxisIds);
        return timed;
    }

    private static TimeSpan RequiredDuration(IReadOnlyDictionary<string, double> from, IReadOnlyDictionary<string, double> to, IEnumerable<AxisDefinition> axes, JointTrajectoryTimingOptions options)
    {
        double seconds = 0d;
        foreach (AxisDefinition axis in axes)
        {
            double distance = Math.Abs(to[axis.Id] - from[axis.Id]);
            if (distance <= double.Epsilon) continue;
            double velocity = axis.Limits.MaximumVelocity * options.VelocityScale;
            double acceleration = axis.Limits.MaximumAcceleration * options.AccelerationScale;
            double deceleration = axis.Limits.MaximumDeceleration * options.AccelerationScale;
            // 对每段按静止到静止的保守下界估算；连续前瞻可在控制器实时插补器进一步优化。
            double velocityDuration = distance / velocity;
            double accelerationDuration = Math.Sqrt(2d * distance / acceleration);
            double decelerationDuration = Math.Sqrt(2d * distance / deceleration);
            seconds = Math.Max(seconds, Math.Max(velocityDuration, accelerationDuration + decelerationDuration));
        }
        return TimeSpan.FromSeconds(Math.Max(seconds, 1e-6d));
    }

    private static IReadOnlyDictionary<string, double> Copy(IReadOnlyDictionary<string, double> values)
        => new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> Zeroes(IEnumerable<string> axisIds)
        => axisIds.ToDictionary(id => id, _ => 0d, StringComparer.OrdinalIgnoreCase);
}
