using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>以最先达到速度上限的关节为准，等比例缩放笛卡尔速度，保持路径方向不变。</summary>
public sealed class CartesianVelocityLimiter : ICartesianVelocityLimiter
{
    public CartesianVelocityLimitResult Limit(KinematicJacobian jacobian, SpatialVelocity requestedVelocity, IReadOnlyCollection<AxisDefinition> axes)
    {
        ArgumentNullException.ThrowIfNull(jacobian);
        ArgumentNullException.ThrowIfNull(axes);
        jacobian.Validate();
        var definitions = axes.ToDictionary(axis => axis.Id, StringComparer.OrdinalIgnoreCase);
        if (definitions.Count != axes.Count || jacobian.JointAxisIds.Any(axisId => !definitions.ContainsKey(axisId)))
            throw new ArgumentException("Jacobian axes must exactly match supplied axis definitions.", nameof(axes));
        IReadOnlyDictionary<string, double> requested = jacobian.Map(requestedVelocity);
        double scale = 1d;
        string? limitingAxis = null;
        foreach ((string axisId, double velocity) in requested)
        {
            double limit = definitions[axisId].Limits.MaximumVelocity;
            if (!double.IsFinite(limit)) continue;
            if (Math.Abs(velocity) > limit)
            {
                double candidate = limit / Math.Abs(velocity);
                if (candidate < scale) { scale = candidate; limitingAxis = axisId; }
            }
        }
        var limited = requested.ToDictionary(item => item.Key, item => item.Value * scale, StringComparer.OrdinalIgnoreCase);
        return new(scale, requested, limited, limitingAxis);
    }
}
