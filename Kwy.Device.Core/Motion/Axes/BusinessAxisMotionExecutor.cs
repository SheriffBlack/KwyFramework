using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>将业务 AxisDefinition.Id 解析为所属运行时与物理轴通道。</summary>
public sealed class BusinessAxisMotionExecutor : IBusinessAxisMotionExecutor
{
    private readonly IMotionRuntimeRegistry runtimes;
    private readonly IAxisDefinitionProvider axisDefinitions;
    private readonly ILogicalIoReader logicalIo;
    private readonly IMotionResourceLock resources;
    private readonly IMotionOperationTracker operations;
    private readonly IAxisHomeLifecycle homeLifecycle;

    public BusinessAxisMotionExecutor(IMotionRuntimeRegistry runtimes, IAxisDefinitionProvider axisDefinitions, ILogicalIoReader logicalIo, IMotionResourceLock resources, IMotionOperationTracker operations, IAxisHomeLifecycle homeLifecycle)
    {
        this.runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        this.axisDefinitions = axisDefinitions ?? throw new ArgumentNullException(nameof(axisDefinitions));
        this.logicalIo = logicalIo ?? throw new ArgumentNullException(nameof(logicalIo));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.homeLifecycle = homeLifecycle ?? throw new ArgumentNullException(nameof(homeLifecycle));
    }

    public Task<MotionCompletionResult> MoveAbsAsync(string axisId, double position, MotionProfile profile, MotionExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        (IMotionDeviceRuntime runtime, AxisDefinition axis) = Resolve(axisId);
        options ??= axis.Defaults.ToExecutionOptions();
        return ExecuteAsync(axis, new(MotionRequestKind.Absolute, new Dictionary<string, double> { [axis.Id] = position }, Profile: profile), () => runtime.AxisExecutor.MoveAbsAsync(axis.Channel, position, profile, options, cancellationToken), cancellationToken);
    }

    public Task<MotionCompletionResult> MoveRelAsync(string axisId, double distance, MotionProfile profile, MotionExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        (IMotionDeviceRuntime runtime, AxisDefinition axis) = Resolve(axisId);
        options ??= axis.Defaults.ToExecutionOptions();
        return ExecuteAsync(axis, new(MotionRequestKind.Relative, new Dictionary<string, double> { [axis.Id] = distance }, Profile: profile), () => runtime.AxisExecutor.MoveRelAsync(axis.Channel, distance, profile, options, cancellationToken), cancellationToken);
    }

    public async Task<SensorSeekResult> SeekSensorAsync(string axisId, string sensorPointId, double velocity, SensorSeekOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sensorPointId);
        (IMotionDeviceRuntime runtime, AxisDefinition axis) = Resolve(axisId);
        options ??= new SensorSeekOptions();
        options.Validate();
        if (!double.IsFinite(velocity) || velocity == 0) throw new ArgumentOutOfRangeException(nameof(velocity));

        using IDisposable lease = await resources.AcquireAsync([axis.Id], cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(axis.Id, new(MotionRequestKind.Jog, SensorPointId: sensorPointId));
        try
        {
            SensorSeekResult result = await runtime.AxisExecutor.SeekSensorAsync(
                axis.Channel,
                sensorPointId,
                () => logicalIo.ReadDi(sensorPointId),
                velocity,
                options,
                cancellationToken).ConfigureAwait(false);
            operations.Complete(operation, MotionOperationState.Succeeded);
            return result;
        }
        catch (OperationCanceledException ex) { operations.Complete(operation, MotionOperationState.Cancelled, ex, "Cancellation requested."); throw; }
        catch (Exception ex) { operations.Complete(operation, MotionOperationState.Failed, ex); throw; }
    }

    public async Task<HomeStatus> HomeAsync(string axisId, CancellationToken cancellationToken = default)
    {
        (IMotionDeviceRuntime runtime, AxisDefinition axis) = Resolve(axisId);
        if (runtime.Card is not IAxisMotionController controller || runtime.Card is not IMotionWaiter waiter)
            throw new NotSupportedException($"Motion card '{runtime.DeviceId}' does not support homing.");
        using IDisposable lease = await resources.AcquireAsync([axis.Id], cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(axis.Id, new(MotionRequestKind.Home));
        try
        {
            homeLifecycle.Begin(axis.Id);
            controller.GoHome(axis.Channel);
            HomeStatus status = await waiter.WaitForHomeCompletedAsync(axis.Channel, axis.Home.Timeout, cancellationToken).ConfigureAwait(false);
            homeLifecycle.Complete(axis.Id, status.State == HomeState.Succeeded, status.ErrorMessage);
            operations.Complete(operation, status.State == HomeState.Succeeded ? MotionOperationState.Succeeded : MotionOperationState.Failed);
            if (status.State != HomeState.Succeeded) throw new MotionHomeException(status);
            return status;
        }
        catch (Exception ex) { operations.Complete(operation, ex is OperationCanceledException ? MotionOperationState.Cancelled : MotionOperationState.Failed, ex); throw; }
    }

    private async Task<MotionCompletionResult> ExecuteAsync(AxisDefinition axis, MotionOperationTarget target, Func<Task<MotionCompletionResult>> execute, CancellationToken cancellationToken)
    {
        using IDisposable lease = await resources.AcquireAsync([axis.Id], cancellationToken).ConfigureAwait(false);
        MotionOperationSnapshot operation = operations.Start(axis.Id, target);
        try 
        { 
            MotionCompletionResult result = await execute().ConfigureAwait(false);
            operations.Complete(operation, MotionOperationState.Succeeded); return result; 
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

    private (IMotionDeviceRuntime Runtime, AxisDefinition Axis) Resolve(string axisId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisId);
        AxisDefinition axis = axisDefinitions.GetRequired(axisId);
        return (runtimes.GetRequired(axis.DeviceId), axis);
    }
}
