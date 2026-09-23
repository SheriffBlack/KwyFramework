using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>将业务运动组命令转换为同一卡上的物理插补调用。</summary>
public sealed class MotionGroupExecutor : IMotionGroupExecutor
{
    private readonly IMotionGroupDefinitionProvider groups;
    private readonly IMotionRuntimeRegistry runtimes;
    private readonly IMotionResourceLock resources;
    private readonly IMotionOperationTracker operations;
    private readonly MotionAdmissionOptions admissionOptions;
    private readonly IAxisHomeLifecycle homes;

    public MotionGroupExecutor(IMotionGroupDefinitionProvider groups, IMotionRuntimeRegistry runtimes, IMotionResourceLock resources, IMotionOperationTracker operations, MotionAdmissionOptions admissionOptions, IAxisHomeLifecycle homes)
    {
        this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
        this.runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.admissionOptions = admissionOptions ?? throw new ArgumentNullException(nameof(admissionOptions));
        this.homes = homes ?? throw new ArgumentNullException(nameof(homes));
    }

    public async Task MoveLinearAsync(LinearMoveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        (MotionGroupDefinition group, IMotionDeviceRuntime runtime, IInterpolationMotionController controller, double[] targets) = Resolve(command.GroupId, command.TargetPositions);
        MotionProfile profile = command.Profile ?? group.DefaultProfile;
        using IDisposable lease = await resources.AcquireAsync(group.AxisIds, cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(group.Id, new(MotionRequestKind.Absolute, command.TargetPositions, Profile: profile));
        try
        {
        double tolerance = command.Tolerance ?? group.DefaultTolerance;
        ValidateExecution(profile, tolerance, command.Timeout);
        ValidateAdmission(group, runtime, targets);
        EnsureLinearPathIsSafe(group, runtime, targets);
        controller.InitCoordinateSystem(group.CoordinateSystemChannel, ResolveChannels(runtime, group));
        controller.MoveLinear(group.CoordinateSystemChannel, targets, profile.Velocity, profile.Acceleration);
        controller.StartInterpolation(group.CoordinateSystemChannel);
        await controller.WaitForCoordinateSystemCompletedAsync(group.CoordinateSystemChannel, targets, tolerance, command.Timeout ?? TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        operations.Complete(operation, MotionOperationState.Succeeded);
        }
        catch (OperationCanceledException ex) { operations.Complete(operation, MotionOperationState.Cancelled, ex, "Cancellation requested."); throw; }
        catch (Exception ex) { operations.Complete(operation, MotionOperationState.Failed, ex); throw; }
    }

    public async Task MoveArcAsync(ArcMoveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        (MotionGroupDefinition group, IMotionDeviceRuntime runtime, IInterpolationMotionController controller, double[] targets) = Resolve(command.GroupId, command.TargetPositions);
        if (group.AxisIds.Count != 2) throw new NotSupportedException("Current physical arc controller supports XY groups only.");
        EnsureExactAxisSet(group, command.CenterPositions, nameof(command.CenterPositions));
        MotionProfile profile = command.Profile ?? group.DefaultProfile;
        using IDisposable lease = await resources.AcquireAsync(group.AxisIds, cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(group.Id, new(MotionRequestKind.Absolute, command.TargetPositions, Profile: profile));
        try
        {
        double tolerance = command.Tolerance ?? group.DefaultTolerance;
        ValidateExecution(profile, tolerance, command.Timeout);
        ValidateAdmission(group, runtime, targets);
        EnsureArcPathIsSafe(group, runtime, targets, command.CenterPositions, command.Direction);
        controller.InitCoordinateSystem(group.CoordinateSystemChannel, ResolveChannels(runtime, group));
        controller.MoveArc(group.CoordinateSystemChannel, targets[0], targets[1], command.CenterPositions[group.AxisIds[0]], command.CenterPositions[group.AxisIds[1]], command.Direction == ArcDirection.Clockwise ? (short)0 : (short)1, profile.Velocity, profile.Acceleration);
        controller.StartInterpolation(group.CoordinateSystemChannel);
        await controller.WaitForCoordinateSystemCompletedAsync(group.CoordinateSystemChannel, targets, tolerance, command.Timeout ?? TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        operations.Complete(operation, MotionOperationState.Succeeded);
        }
        catch (OperationCanceledException ex) { operations.Complete(operation, MotionOperationState.Cancelled, ex, "Cancellation requested."); throw; }
        catch (Exception ex) { operations.Complete(operation, MotionOperationState.Failed, ex); throw; }
    }

    private (MotionGroupDefinition, IMotionDeviceRuntime, IInterpolationMotionController, double[]) Resolve(string groupId, IReadOnlyDictionary<string, double> values)
    {
        MotionGroupDefinition group = groups.GetMotionGroup(groupId);
        group.Validate(); EnsureExactAxisSet(group, values, nameof(values));
        IMotionDeviceRuntime runtime = runtimes.GetRequired(group.DeviceId);
        if (runtime.Card is not IInterpolationMotionController controller || runtime.Card is not IAxisDefinitionProvider axes)
            throw new NotSupportedException($"Motion card '{group.DeviceId}' does not support configured interpolation.");
        foreach (string axisId in group.AxisIds)
        {
            AxisDefinition axis = axes.Axes.SingleOrDefault(item => string.Equals(item.Id, axisId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Axis '{axisId}' is not defined on card '{group.DeviceId}'.");
            if (!string.Equals(axis.DeviceId, group.DeviceId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Axis '{axisId}' belongs to another device.");
        }
        var physical = ResolveChannels(runtime, group);
        var positions = group.AxisIds.Select(id => values[id]).ToArray();
        var map = group.AxisIds.Zip(positions).ToDictionary(item => item.First, item => item.Second, StringComparer.OrdinalIgnoreCase);
        if (group.ForbiddenZones.Any(zone => zone.Contains(map))) throw new MotionAdmissionDeniedException([new("MotionGroupForbiddenZone", $"Target enters a forbidden zone in group '{group.Id}'.")]);
        return (group, runtime, controller, positions);
    }

    private static short[] ResolveChannels(IMotionDeviceRuntime runtime, MotionGroupDefinition group)
    {
        IAxisDefinitionProvider axes = (IAxisDefinitionProvider)runtime.Card;
        return group.AxisIds.Select(id => axes.Axes.Single(axis => string.Equals(axis.Id, id, StringComparison.OrdinalIgnoreCase)).Channel).ToArray();
    }

    private void ValidateAdmission(MotionGroupDefinition group, IMotionDeviceRuntime runtime, IReadOnlyList<double> targets)
    {
        IAxisDefinitionProvider definitions = (IAxisDefinitionProvider)runtime.Card;
        var guard = new MotionAdmissionGuard(runtime.Card, runtime.StateMonitor, admissionOptions, homes);
        for (int index = 0; index < group.AxisIds.Count; index++)
        {
            AxisDefinition axis = definitions.Axes.Single(item => string.Equals(item.Id, group.AxisIds[index], StringComparison.OrdinalIgnoreCase));
            double current = runtime.StateMonitor.GetAxisSnapshot(axis.Channel).Position;
            guard.ValidateAndThrow(new(axis.Channel, MotionRequestKind.Absolute, targets[index], Math.Sign(targets[index] - current)));
        }
    }

    private static void EnsureExactAxisSet(MotionGroupDefinition group, IReadOnlyDictionary<string, double> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != group.AxisIds.Count || group.AxisIds.Any(id => !values.ContainsKey(id)) || values.Values.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Targets must contain every group axis exactly once with finite values.", parameterName);
    }

    private static void ValidateExecution(MotionProfile profile, double tolerance, TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (timeout is { } value && value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    private static void EnsureLinearPathIsSafe(MotionGroupDefinition group, IMotionDeviceRuntime runtime, double[] targets)
    {
        short[] channels = ResolveChannels(runtime, group);
        var start = group.AxisIds.Zip(channels).ToDictionary(item => item.First, item => runtime.StateMonitor.GetAxisSnapshot(item.Second).Position, StringComparer.OrdinalIgnoreCase);
        var end = group.AxisIds.Zip(targets).ToDictionary(item => item.First, item => item.Second, StringComparer.OrdinalIgnoreCase);
        foreach (MultiAxisForbiddenZone zone in group.ForbiddenZones)
            if (IntersectsBox(start, end, zone.Bounds))
                throw ForbiddenPath(group, zone);
    }

    private static void EnsureArcPathIsSafe(MotionGroupDefinition group, IMotionDeviceRuntime runtime, double[] targets, IReadOnlyDictionary<string, double> center, ArcDirection direction)
    {
        short[] channels = ResolveChannels(runtime, group);
        var previous = group.AxisIds.Zip(channels).ToDictionary(item => item.First, item => runtime.StateMonitor.GetAxisSnapshot(item.Second).Position, StringComparer.OrdinalIgnoreCase);
        double cx = center[group.AxisIds[0]], cy = center[group.AxisIds[1]];
        double startAngle = Math.Atan2(previous[group.AxisIds[1]] - cy, previous[group.AxisIds[0]] - cx);
        double endAngle = Math.Atan2(targets[1] - cy, targets[0] - cx);
        double delta = endAngle - startAngle;
        if (direction == ArcDirection.Clockwise && delta >= 0) delta -= 2 * Math.PI;
        if (direction == ArcDirection.CounterClockwise && delta <= 0) delta += 2 * Math.PI;
        double radius = Math.Sqrt(Math.Pow(previous[group.AxisIds[0]] - cx, 2) + Math.Pow(previous[group.AxisIds[1]] - cy, 2));
        // 控制器圆弧能力为 XY；以 64 段圆弧折线验证，避免仅检查终点遗漏禁入区。
        for (int step = 1; step <= 64; step++)
        {
            double ratio = step / 64d;
            var current = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [group.AxisIds[0]] = cx + radius * Math.Cos(startAngle + delta * ratio),
                [group.AxisIds[1]] = cy + radius * Math.Sin(startAngle + delta * ratio)
            };
            foreach (MultiAxisForbiddenZone zone in group.ForbiddenZones)
                if (IntersectsBox(previous, current, zone.Bounds)) throw ForbiddenPath(group, zone);
            previous = current;
        }
    }

    private static bool IntersectsBox(IReadOnlyDictionary<string, double> start, IReadOnlyDictionary<string, double> end, IReadOnlyDictionary<string, AxisForbiddenRange> box)
    {
        double enter = 0, exit = 1;
        foreach ((string axis, AxisForbiddenRange range) in box)
        {
            if (!start.TryGetValue(axis, out double from) || !end.TryGetValue(axis, out double to)) return false;
            double delta = to - from;
            if (Math.Abs(delta) < double.Epsilon) { if (!range.Contains(from)) return false; continue; }
            double first = (range.Minimum - from) / delta, last = (range.Maximum - from) / delta;
            if (first > last) (first, last) = (last, first);
            enter = Math.Max(enter, first); exit = Math.Min(exit, last);
            if (enter > exit) return false;
        }
        return exit >= 0 && enter <= 1;
    }

    private static MotionAdmissionDeniedException ForbiddenPath(MotionGroupDefinition group, MultiAxisForbiddenZone zone)
        => new([new MotionAdmissionViolation("MotionGroupForbiddenPath", $"Path of group '{group.Id}' crosses forbidden zone '{zone.Id}'.")]);
}
