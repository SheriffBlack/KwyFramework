using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>加载并校验设备坐标系树。</summary>
public sealed class CoordinateTransformService : ICoordinateFrameRegistry
{
    private readonly object sync = new();
    private IReadOnlyDictionary<string, CoordinateFrameDefinition> frames;
    private long version;

    public CoordinateTransformService(IEnumerable<CoordinateFrameDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var map = new Dictionary<string, CoordinateFrameDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (CoordinateFrameDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!map.TryAdd(definition.Id, definition)) throw new InvalidOperationException($"Duplicate coordinate frame ID '{definition.Id}'.");
        }
        foreach (CoordinateFrameDefinition definition in map.Values)
            if (definition.ParentFrameId is { } parent && !map.ContainsKey(parent))
                throw new InvalidOperationException($"Coordinate frame '{definition.Id}' references missing parent '{parent}'.");
        frames = map;
        foreach (string id in frames.Keys) _ = ToRoot(id, frames);
    }

    public long Version => Interlocked.Read(ref version);

    public Pose6D Transform(Pose6D pose, string sourceFrameId, string targetFrameId)
    {
        pose.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFrameId);
        IReadOnlyDictionary<string, CoordinateFrameDefinition> snapshot = frames;
        RigidPose sourceToRoot = ToRoot(sourceFrameId, snapshot);
        RigidPose targetToRoot = ToRoot(targetFrameId, snapshot);
        return Pose6D.FromRigidPose(RigidPose.Compose(RigidPose.Inverse(targetToRoot), RigidPose.Compose(sourceToRoot, pose.ToRigidPose())));
    }

    public void UpdateDynamicFrame(string frameId, Pose6D transformToParent)
    {
        transformToParent.Validate();
        lock (sync)
        {
            if (!frames.TryGetValue(frameId, out CoordinateFrameDefinition? frame)) throw new KeyNotFoundException($"Coordinate frame '{frameId}' was not found.");
            if (!frame.IsDynamic) throw new InvalidOperationException($"Coordinate frame '{frameId}' is fixed and cannot be updated at runtime.");
            var copy = new Dictionary<string, CoordinateFrameDefinition>(frames, StringComparer.OrdinalIgnoreCase) { [frameId] = frame with { TransformToParent = transformToParent } };
            foreach (string id in copy.Keys) _ = ToRoot(id, copy);
            frames = copy;
            Interlocked.Increment(ref version);
        }
    }

    private static RigidPose ToRoot(string frameId, IReadOnlyDictionary<string, CoordinateFrameDefinition> frames)
    {
        if (!frames.TryGetValue(frameId, out CoordinateFrameDefinition? frame))
            throw new KeyNotFoundException($"Coordinate frame '{frameId}' was not found.");
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RigidPose value = RigidPose.Identity;
        for (CoordinateFrameDefinition? current = frame; current is not null;)
        {
            if (!visited.Add(current.Id)) throw new InvalidOperationException($"Coordinate frame cycle detected at '{current.Id}'.");
            value = RigidPose.Compose(current.TransformToParent.ToRigidPose(), value);
            current = current.ParentFrameId is { } parent ? frames[parent] : null;
        }
        return value;
    }

}

/// <summary>将工艺空间位姿转换为机构关节目标，再复用已有插补组执行器。</summary>
public sealed class PoseMotionExecutor : IPoseMotionExecutor
{
    private readonly IReadOnlyDictionary<string, KinematicMechanismDefinition> mechanisms;
    private readonly IReadOnlyDictionary<string, IKinematicsSolver> solvers;
    private readonly ICoordinateTransformService coordinates;
    private readonly IMotionGroupDefinitionProvider groups;
    private readonly IMotionGroupExecutor motionGroups;
    private readonly IMotionRuntimeRegistry runtimes;

    public PoseMotionExecutor(IEnumerable<KinematicMechanismDefinition> definitions, IEnumerable<IKinematicsSolver> solvers, ICoordinateTransformService coordinates, IMotionGroupDefinitionProvider groups, IMotionGroupExecutor motionGroups, IMotionRuntimeRegistry runtimes)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.solvers = solvers?.ToDictionary(item => item.MechanismId, StringComparer.OrdinalIgnoreCase) ?? throw new ArgumentNullException(nameof(solvers));
        mechanisms = definitions.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        foreach (KinematicMechanismDefinition definition in mechanisms.Values) definition.Validate();
        this.coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
        this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
        this.motionGroups = motionGroups ?? throw new ArgumentNullException(nameof(motionGroups));
        this.runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
    }

    public Task MoveToPoseAsync(string mechanismId, string sourceFrameId, Pose6D targetPose, MotionProfile? profile = null, double? tolerance = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        targetPose.Validate();
        if (!mechanisms.TryGetValue(mechanismId, out KinematicMechanismDefinition? mechanism)) throw new KeyNotFoundException($"Kinematic mechanism '{mechanismId}' was not found.");
        if (!solvers.TryGetValue(mechanismId, out IKinematicsSolver? solver)) throw new NotSupportedException($"Kinematic mechanism '{mechanismId}' has no solver.");
        Pose6D poseInBase = coordinates.Transform(targetPose, sourceFrameId, mechanism.BaseFrameId);
        MotionGroupDefinition group = groups.GetMotionGroup(mechanism.MotionGroupId);
        if (!group.AxisIds.SequenceEqual(mechanism.JointAxisIds, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Kinematic mechanism '{mechanismId}' joint axes do not match motion group '{group.Id}'.");
        IMotionDeviceRuntime runtime = runtimes.GetRequired(group.DeviceId);
        if (runtime.Card is not IAxisDefinitionProvider definitions) throw new InvalidOperationException($"Motion group '{group.Id}' card has no axis definitions.");
        IReadOnlyDictionary<string, double> joints = KinematicSolutionSelector.Select(solver.SolveInverseCandidates(poseInBase.ToRigidPose()), mechanism, group, runtime, definitions);
        return motionGroups.MoveLinearAsync(new(group.Id, joints, profile, tolerance, timeout), cancellationToken);
    }
}
