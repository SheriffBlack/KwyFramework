using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>在可达性、奇异度、软限位和当前关节位置约束下选择连续性最好的逆解。</summary>
internal static class KinematicSolutionSelector
{
    public static IReadOnlyDictionary<string, double> Select(IReadOnlyList<KinematicSolution> candidates, KinematicMechanismDefinition mechanism, MotionGroupDefinition group, IMotionDeviceRuntime runtime, IAxisChannelDefinitionProvider definitions, IReadOnlyDictionary<string, double>? referencePositions = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var eligible = new List<(IReadOnlyDictionary<string, double> Joints, double Score)>();
        foreach (KinematicSolution candidate in candidates)
        {
            if (!candidate.IsReachable || candidate.SingularityMetric is { } metric && metric < mechanism.MinimumSingularityMetric) continue;
            if (candidate.JointPositions.Count != group.AxisIds.Count || group.AxisIds.Any(id => !candidate.JointPositions.TryGetValue(id, out double value) || !double.IsFinite(value))) continue;
            double score = 0; bool inLimits = true;
            foreach (string axisId in group.AxisIds)
            {
                AxisDefinition axis = definitions.Axes.Single(item => string.Equals(item.Id, axisId, StringComparison.OrdinalIgnoreCase));
                double target = candidate.JointPositions[axisId];
                if (target < axis.Limits.MinimumPosition || target > axis.Limits.MaximumPosition) { inLimits = false; break; }
                double current = referencePositions is not null && referencePositions.TryGetValue(axisId, out double reference)
                    ? reference
                    : runtime.StateMonitor.GetAxisSnapshot(axis.Channel).Position;
                double span = axis.Limits.MaximumPosition - axis.Limits.MinimumPosition;
                double normalization = double.IsFinite(span) && span > 0 ? span : Math.Max(Math.Max(Math.Abs(current), Math.Abs(target)), 1d);
                score += Math.Pow((target - current) / normalization, 2);
            }
            if (inLimits) eligible.Add((candidate.JointPositions, score));
        }
        return eligible.OrderBy(item => item.Score).Select(item => item.Joints).FirstOrDefault()
            ?? throw new MotionAdmissionDeniedException([new("KinematicsUnreachable", $"No reachable, non-singular and in-limit inverse solution exists for mechanism '{mechanism.Id}'.")]);
    }
}
