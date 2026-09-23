using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>首个规划管线：将多段笛卡尔直线转换为已校验的关节轨迹，不直接下发控制器。</summary>
public sealed class MotionPlanningPipeline : IMotionPlanningPipeline
{
    private readonly IReadOnlyDictionary<string, KinematicMechanismDefinition> mechanisms;
    private readonly IReadOnlyDictionary<string, IKinematicsSolver> solvers;
    private readonly ICoordinateFrameRegistry frames;
    private readonly IMotionGroupDefinitionProvider groups;
    private readonly IMotionRuntimeRegistry runtimes;
    private readonly ICartesianTrajectoryPlanner cartesian;

    public MotionPlanningPipeline(IEnumerable<KinematicMechanismDefinition> definitions, IEnumerable<IKinematicsSolver> solvers, ICoordinateFrameRegistry frames, IMotionGroupDefinitionProvider groups, IMotionRuntimeRegistry runtimes, ICartesianTrajectoryPlanner cartesian)
    {
        mechanisms = definitions?.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase) ?? throw new ArgumentNullException(nameof(definitions));
        this.solvers = solvers?.ToDictionary(item => item.MechanismId, StringComparer.OrdinalIgnoreCase) ?? throw new ArgumentNullException(nameof(solvers));
        this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
        this.runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        this.cartesian = cartesian ?? throw new ArgumentNullException(nameof(cartesian));
    }

    public Task<JointTrajectory> PlanAsync(MotionProgram program, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program); program.Validate();
        long frameVersion = frames.Version;
        if (!mechanisms.TryGetValue(program.MechanismId, out KinematicMechanismDefinition? mechanism)) throw new KeyNotFoundException($"Kinematic mechanism '{program.MechanismId}' was not found.");
        if (!solvers.TryGetValue(mechanism.Id, out IKinematicsSolver? solver)) throw new NotSupportedException($"Kinematic mechanism '{mechanism.Id}' has no solver.");
        MotionGroupDefinition group = groups.GetMotionGroup(mechanism.MotionGroupId);
        if (!group.AxisIds.SequenceEqual(mechanism.JointAxisIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"Kinematic mechanism '{mechanism.Id}' joint axes do not match motion group '{group.Id}'.");
        IMotionDeviceRuntime runtime = runtimes.GetRequired(group.DeviceId);
        if (runtime.Card is not IAxisDefinitionProvider axisDefinitions) throw new InvalidOperationException($"Motion group '{group.Id}' card has no axis definitions.");

        var points = new List<JointTrajectoryPoint>();
        IReadOnlyDictionary<string, double>? previousJoints = null;
        Pose6D start = program.StartPose;
        TimeSpan offset = TimeSpan.Zero;
        foreach (CartesianTrajectorySegment segment in program.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (segment is not CartesianLineSegment) throw new NotSupportedException($"Cartesian segment '{segment.Id}' is not supported by the current planning pipeline.");
            CartesianTrajectory path = cartesian.PlanLinear(start, segment.EndPose, program.Profile);
            foreach (CartesianTrajectoryPoint point in path.Points.Skip(points.Count == 0 ? 0 : 1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Pose6D inBase = frames.Transform(Pose6D.FromRigidPose(point.Pose), program.SourceFrameId, mechanism.BaseFrameId);
                IReadOnlyDictionary<string, double> joints = KinematicSolutionSelector.Select(solver.SolveInverseCandidates(inBase.ToRigidPose()), mechanism, group, runtime, axisDefinitions, previousJoints);
                previousJoints = new Dictionary<string, double>(joints, StringComparer.OrdinalIgnoreCase);
                points.Add(new(offset + point.TimeFromStart, previousJoints));
            }
            offset += path.Points[^1].TimeFromStart;
            start = segment.EndPose;
        }
        if (frames.Version != frameVersion) throw new InvalidOperationException("Coordinate frames changed while planning; retry with a stable frame snapshot.");
        var trajectory = new JointTrajectory(mechanism.Id, frameVersion, points);
        trajectory.Validate(group.AxisIds);
        return Task.FromResult(trajectory);
    }
}
