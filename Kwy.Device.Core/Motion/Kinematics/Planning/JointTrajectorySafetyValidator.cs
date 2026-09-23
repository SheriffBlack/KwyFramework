using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>按完整关节路径校验轴软限位与多轴禁入区，供控制器原生连续轨迹下发前预检使用。</summary>
public sealed class JointTrajectorySafetyValidator : IJointTrajectorySafetyValidator
{
    public void Validate(JointTrajectory trajectory, MotionGroupDefinition group, IAxisDefinitionProvider axisDefinitions)
    {
        ArgumentNullException.ThrowIfNull(trajectory);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(axisDefinitions);
        trajectory.Validate(group.AxisIds);
        AxisDefinition[] axes = group.AxisIds.Select(axisId => axisDefinitions.Axes.SingleOrDefault(axis => string.Equals(axis.Id, axisId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Axis '{axisId}' is not defined on the motion card.")).ToArray();

        IReadOnlyDictionary<string, double>? previous = null;
        foreach (JointTrajectoryPoint point in trajectory.Points)
        {
            EnsureAxisLimits(point.Positions, axes);
            foreach (MultiAxisForbiddenZone zone in group.ForbiddenZones)
            {
                if (zone.Contains(point.Positions)) throw ForbiddenPoint(group, zone);
                if (previous is not null && IntersectsBox(previous, point.Positions, zone.Bounds)) throw ForbiddenPath(group, zone);
            }
            previous = point.Positions;
        }
    }

    private static void EnsureAxisLimits(IReadOnlyDictionary<string, double> positions, IEnumerable<AxisDefinition> axes)
    {
        foreach (AxisDefinition axis in axes)
        {
            double position = positions[axis.Id];
            if (position < axis.Limits.MinimumPosition || position > axis.Limits.MaximumPosition)
                throw new MotionAdmissionDeniedException([new("JointTrajectorySoftLimit", $"Axis '{axis.Id}' target {position} exceeds configured soft limits.")]);
        }
    }

    private static bool IntersectsBox(IReadOnlyDictionary<string, double> start, IReadOnlyDictionary<string, double> end, IReadOnlyDictionary<string, AxisForbiddenRange> box)
    {
        double enter = 0d, exit = 1d;
        foreach ((string axisId, AxisForbiddenRange range) in box)
        {
            if (!start.TryGetValue(axisId, out double from) || !end.TryGetValue(axisId, out double to)) return false;
            double delta = to - from;
            if (Math.Abs(delta) < double.Epsilon)
            {
                if (!range.Contains(from)) return false;
                continue;
            }
            double first = (range.Minimum - from) / delta;
            double last = (range.Maximum - from) / delta;
            if (first > last) (first, last) = (last, first);
            enter = Math.Max(enter, first);
            exit = Math.Min(exit, last);
            if (enter > exit) return false;
        }
        return exit >= 0d && enter <= 1d;
    }

    private static MotionAdmissionDeniedException ForbiddenPoint(MotionGroupDefinition group, MultiAxisForbiddenZone zone)
        => new([new("JointTrajectoryForbiddenZone", $"Trajectory of group '{group.Id}' enters forbidden zone '{zone.Id}'.")]);

    private static MotionAdmissionDeniedException ForbiddenPath(MotionGroupDefinition group, MultiAxisForbiddenZone zone)
        => new([new("JointTrajectoryForbiddenPath", $"Trajectory of group '{group.Id}' crosses forbidden zone '{zone.Id}'.")]);
}
