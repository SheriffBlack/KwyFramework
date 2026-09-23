using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 连续轮廓程序的设备级门面。
/// 上位机在这里完成配置、回零、实时状态、坐标版本及完整路径预检；实际轨迹周期由控制器原生内核执行。
/// </summary>
public sealed class ControllerMotionProgramService : IControllerMotionProgramService
{
    private readonly IReadOnlyDictionary<string, KinematicMechanismDefinition> mechanisms;
    private readonly IMotionPlanningPipeline planning;
    private readonly ICoordinateFrameRegistry frames;
    private readonly IMotionGroupDefinitionProvider groups;
    private readonly IMotionRuntimeRegistry runtimes;
    private readonly IMotionResourceLock resources;
    private readonly IMotionOperationTracker operations;
    private readonly IAxisHomeLifecycle homes;
    private readonly IJointTrajectorySafetyValidator trajectorySafety;
    private readonly MotionAdmissionOptions admissionOptions;

    public ControllerMotionProgramService(
        IEnumerable<KinematicMechanismDefinition> mechanisms,
        IMotionPlanningPipeline planning,
        ICoordinateFrameRegistry frames,
        IMotionGroupDefinitionProvider groups,
        IMotionRuntimeRegistry runtimes,
        IMotionResourceLock resources,
        IMotionOperationTracker operations,
        IAxisHomeLifecycle homes,
        IJointTrajectorySafetyValidator trajectorySafety,
        MotionAdmissionOptions admissionOptions)
    {
        this.mechanisms = mechanisms?.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(mechanisms));
        this.planning = planning ?? throw new ArgumentNullException(nameof(planning));
        this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
        this.runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.homes = homes ?? throw new ArgumentNullException(nameof(homes));
        this.trajectorySafety = trajectorySafety ?? throw new ArgumentNullException(nameof(trajectorySafety));
        this.admissionOptions = admissionOptions ?? throw new ArgumentNullException(nameof(admissionOptions));
    }

    public async Task ExecuteAsync(
        MotionProgram program,
        ControllerMotionProgramRequirements? requirements = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        program.Validate();
        requirements ??= new ControllerMotionProgramRequirements();

        if (!mechanisms.TryGetValue(program.MechanismId, out KinematicMechanismDefinition? mechanism))
            throw new KeyNotFoundException($"Kinematic mechanism '{program.MechanismId}' was not found.");

        MotionGroupDefinition group = groups.GetMotionGroup(mechanism.MotionGroupId);
        IMotionDeviceRuntime runtime = runtimes.GetRequired(group.DeviceId);
        if (runtime.Card is not IAxisDefinitionProvider axisDefinitions)
            throw new NotSupportedException($"Motion card '{runtime.DeviceId}' does not provide axis definitions.");
        if (runtime.Card is not IControllerMotionProgramAdapter adapter)
        {
            throw new NotSupportedException($"Motion card '{runtime.DeviceId}' does not provide a native controller-motion-program adapter.");
        }

        using IDisposable lease = await resources.AcquireAsync(group.AxisIds, cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(
            group.Id,
            new(MotionRequestKind.Absolute, ProgramId: program.Id));
        try
        {
            requirements.ValidateAgainst(adapter.Capabilities);
            JointTrajectory preflight = await planning.PlanAsync(program, cancellationToken).ConfigureAwait(false);
            trajectorySafety.Validate(preflight, group, axisDefinitions);
            ValidateAdmission(group, runtime, axisDefinitions, preflight.Points[^1].Positions);
            EnsureStartPositionMatchesRuntime(preflight, group, runtime, axisDefinitions);
            if (frames.Version != preflight.CoordinateFrameVersion)
                throw new InvalidOperationException("Coordinate frames changed during preflight; retry with a stable frame snapshot.");

            var request = new ControllerMotionProgramRequest(
                runtime.DeviceId,
                group.Id,
                program,
                preflight.CoordinateFrameVersion,
                requirements,
                preflight);
            request.Validate();
            IControllerMotionProgram compiled = await adapter.CompileAsync(request, cancellationToken).ConfigureAwait(false);
            ValidateCompiledProgram(compiled, request);
            if (frames.Version != preflight.CoordinateFrameVersion)
                throw new InvalidOperationException("Coordinate frames changed before controller program start; retry with a stable frame snapshot.");

            await adapter.ExecuteAsync(compiled, cancellationToken).ConfigureAwait(false);
            operations.Complete(operation, MotionOperationState.Succeeded);
        }
        catch (OperationCanceledException ex)
        {
            operations.Complete(operation, MotionOperationState.Cancelled, ex, "Cancellation requested.");
            throw;
        }
        catch (Exception ex)
        {
            operations.Complete(operation, MotionOperationState.Failed, ex);
            throw;
        }
    }

    private void ValidateAdmission(MotionGroupDefinition group, IMotionDeviceRuntime runtime, IAxisDefinitionProvider definitions, IReadOnlyDictionary<string, double> targets)
    {
        var guard = new MotionAdmissionGuard(runtime.Card, runtime.StateMonitor, admissionOptions, homes);
        foreach (string axisId in group.AxisIds)
        {
            AxisDefinition axis = FindAxis(definitions, axisId);
            double current = runtime.StateMonitor.GetAxisSnapshot(axis.Channel).Position;
            double target = targets[axis.Id];
            guard.ValidateAndThrow(new(axis.Channel, MotionRequestKind.Absolute, target, Math.Sign(target - current)));
        }
    }

    private static void EnsureStartPositionMatchesRuntime(JointTrajectory trajectory, MotionGroupDefinition group, IMotionDeviceRuntime runtime, IAxisDefinitionProvider definitions)
    {
        JointTrajectoryPoint first = trajectory.Points[0];
        foreach (string axisId in group.AxisIds)
        {
            AxisDefinition axis = FindAxis(definitions, axisId);
            double actual = runtime.StateMonitor.GetAxisSnapshot(axis.Channel).Position;
            double planned = first.Positions[axis.Id];
            if (Math.Abs(actual - planned) > group.DefaultTolerance)
            {
                throw new MotionAdmissionDeniedException([new(
                    "ProgramStartPositionMismatch",
                    $"Axis '{axis.Id}' is at {actual}, but program '{trajectory.MechanismId}' expects {planned}.")]);
            }
        }
    }

    private static AxisDefinition FindAxis(IAxisDefinitionProvider definitions, string axisId)
        => definitions.Axes.SingleOrDefault(item => string.Equals(item.Id, axisId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Axis '{axisId}' is not defined on the motion card.");

    private static void ValidateCompiledProgram(IControllerMotionProgram compiled, ControllerMotionProgramRequest request)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        if (string.IsNullOrWhiteSpace(compiled.Id)
            || !string.Equals(compiled.DeviceId, request.DeviceId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(compiled.MotionGroupId, request.MotionGroupId, StringComparison.OrdinalIgnoreCase)
            || compiled.CoordinateFrameVersion != request.CoordinateFrameVersion)
        {
            throw new InvalidOperationException("The controller adapter returned a program that does not match the compilation request.");
        }
    }
}
