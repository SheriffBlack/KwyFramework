using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 动作发出前的设备级准入配置。
/// 仅用于拦截离线、未回零、限位或静态约束冲突等明显错误，不能替代控制器和安全硬件的实时保护。
/// </summary>
public sealed class MotionAdmissionOptions
{
    public TimeSpan MaximumSnapshotAge { get; set; } = TimeSpan.FromMilliseconds(500);

    public bool RequireServoEnabled { get; set; } = true;

    public bool RequireHomedForPositioning { get; set; } = true;

    public IDictionary<short, (double Negative, double Positive)> SoftwareLimits { get; }
        = new Dictionary<short, (double Negative, double Positive)>();

    public IList<Func<MotionRequest, MotionAdmissionViolation?>> AdditionalRules { get; }
        = new List<Func<MotionRequest, MotionAdmissionViolation?>>();
}

/// <summary>按轴快照、回零可信度和静态约束执行运动准入的统一守卫。</summary>
public sealed class MotionAdmissionGuard : IMotionAdmissionGuard
{
    private readonly IMotionCard card;
    private readonly IMotionStateProvider stateProvider;
    private readonly MotionAdmissionOptions options;
    private readonly IAxisChannelDefinitionProvider? axisDefinitions;
    private readonly IAxisHomeLifecycle? homeLifecycle;

    public MotionAdmissionGuard(IMotionCard card, IMotionStateProvider stateProvider, MotionAdmissionOptions options, IAxisHomeLifecycle? homeLifecycle = null)
    {
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        this.stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        axisDefinitions = card as IAxisChannelDefinitionProvider;
        this.homeLifecycle = homeLifecycle;
    }

    public MotionAdmissionResult Validate(MotionRequest request)
    {
        List<MotionAdmissionViolation>? violations = null;

        if (!card.IsConnected)
        {
            return new(new[] { new MotionAdmissionViolation("NotConnected", "Motion card is not connected.") });
        }

        MotionAxisSnapshot snapshot;
        try
        {
            snapshot = stateProvider.GetAxisSnapshot(request.Axis);
        }
        catch (KeyNotFoundException)
        {
            return new(new[] { new MotionAdmissionViolation("NoSnapshot", $"Axis {request.Axis} has no state snapshot.") });
        }

        if (DateTimeOffset.Now - snapshot.Timestamp > options.MaximumSnapshotAge)
        {
            AddViolation(ref violations, "StaleSnapshot", $"Axis {request.Axis} state snapshot is stale.");
        }

        if (snapshot.IsAlarm)
        {
            AddViolation(ref violations, "AxisAlarm", $"Axis {request.Axis} is in alarm state.");
        }

        if (snapshot.Fault is { Severity: AxisFaultSeverity.StopRequired or AxisFaultSeverity.SafetyCritical } fault)
        {
            AddViolation(ref violations, "ControllerFault", $"Axis {request.Axis} reports controller fault '{fault.Code}': {fault.Message}");
        }

        if (options.RequireServoEnabled && !snapshot.IsServoEnabled)
        {
            AddViolation(ref violations, "ServoDisabled", $"Axis {request.Axis} servo is disabled.");
        }

        bool requiresHomed = request.RequiresHomed
            && request.Kind is MotionRequestKind.Absolute or MotionRequestKind.Relative
            && options.RequireHomedForPositioning;
        if (requiresHomed && snapshot.HomeState != HomeState.Succeeded)
        {
            AddViolation(ref violations, "NotHomed", $"Axis {request.Axis} has not completed homing.");
        }
        if (requiresHomed && axisDefinitions is not null && homeLifecycle is not null)
        {
            AxisHomeLifecycleSnapshot lifecycle = homeLifecycle.Get(axisDefinitions.GetAxisDefinition(request.Axis).Id);
            if (lifecycle.Validity != AxisHomeValidity.Valid)
                AddViolation(ref violations, "HomeValidity", $"Axis {request.Axis} homing is not trusted. State={lifecycle.Validity}.");
        }

        if (request.Direction > 0 && snapshot.IsPositiveLimit)
        {
            AddViolation(ref violations, "PositiveLimit", $"Axis {request.Axis} positive limit is active.");
        }

        if (request.Direction < 0 && snapshot.IsNegativeLimit)
        {
            AddViolation(ref violations, "NegativeLimit", $"Axis {request.Axis} negative limit is active.");
        }

        if (request.TargetPosition is double target
            && options.SoftwareLimits.TryGetValue(request.Axis, out var limits)
            && (target < limits.Negative || target > limits.Positive))
        {
            AddViolation(ref violations, "SoftwareLimit", $"Axis {request.Axis} target {target} is outside [{limits.Negative}, {limits.Positive}].");
        }

        if (request.TargetPosition is double definitionTarget && axisDefinitions is not null)
        {
            AxisDefinition definition = axisDefinitions.GetAxisDefinition(request.Axis);
            if (definition.Safety.ForbiddenRanges.Any(range =>
                    range.Contains(definitionTarget)
                    || !range.Contains(snapshot.Position)
                    && Math.Min(snapshot.Position, definitionTarget) <= range.Maximum
                    && Math.Max(snapshot.Position, definitionTarget) >= range.Minimum))
            {
                AddViolation(ref violations, "ForbiddenRange", $"Axis {request.Axis} path to {definitionTarget} enters or crosses a forbidden range.");
            }
            if (definition.Rotary?.IsForbidden(definitionTarget) == true)
                AddViolation(ref violations, "ForbiddenAngle", $"Axis {request.Axis} target {definitionTarget} is inside a forbidden angle range.");
        }

        foreach (var rule in options.AdditionalRules)
        {
            MotionAdmissionViolation? violation = rule(request);
            if (violation is not null)
            {
                (violations ??= new List<MotionAdmissionViolation>()).Add(violation);
            }
        }

        return violations is null ? MotionAdmissionResult.Allowed : new(violations);
    }

    public void ValidateAndThrow(MotionRequest request)
    {
        MotionAdmissionResult result = Validate(request);
        if (!result.IsAllowed)
        {
            throw new MotionAdmissionDeniedException(result.Violations);
        }
    }

    private static void AddViolation(ref List<MotionAdmissionViolation>? violations, string code, string message)
        => (violations ??= new List<MotionAdmissionViolation>()).Add(new(code, message));
}

/// <summary>
/// 为底层单轴控制器补充动作前准入检查的适配器。
/// 供 Core 基础设施使用，工艺代码仍应依赖业务轴执行器而不是物理轴通道。
/// </summary>
public sealed class AdmittedAxisMotionController : IAdmittedAxisMotionController
{
    private readonly IAxisMotionController inner;
    private readonly IMotionProfileController profileController;
    private readonly IAxisStatusReader statusReader;
    private readonly IMotionAdmissionGuard safetyGuard;
    private readonly IAxisChannelDefinitionProvider? definitions;
    private readonly IAxisHomeLifecycle? homeLifecycle;

    public AdmittedAxisMotionController(
        IAxisMotionController inner,
        IMotionProfileController profileController,
        IAxisStatusReader statusReader,
        IMotionAdmissionGuard safetyGuard,
        IAxisChannelDefinitionProvider? definitions = null,
        IAxisHomeLifecycle? homeLifecycle = null)
    {
        this.inner = inner;
        this.profileController = profileController;
        this.statusReader = statusReader;
        this.safetyGuard = safetyGuard;
        this.definitions = definitions;
        this.homeLifecycle = homeLifecycle;
    }

    public void ServoOn(short axis) => inner.ServoOn(axis);
    public void ServoOff(short axis)
    {
        inner.ServoOff(axis);
        InvalidateHome(axis, AxisHomeInvalidationReason.ServoDisabled);
    }
    public void ClearError(short axis)
    {
        inner.ClearError(axis);
        InvalidateHome(axis, AxisHomeInvalidationReason.AlarmReset);
    }
    public void Stop(short axis) => inner.Stop(axis);
    public void Abort(short axis) => inner.Abort(axis);
    public void SetSoftLimit(short axis, double positive, double negative) => inner.SetSoftLimit(axis, positive, negative);

    public void MoveAbs(short axis, double position, MotionProfile profile)
    {
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Absolute, position, Math.Sign(position - statusReader.GetPosition(axis))));
        profileController.MoveAbs(axis, position, profile);
    }

    public void MoveRel(short axis, double distance, MotionProfile profile)
    {
        double target = statusReader.GetPosition(axis) + distance;
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Relative, target, Math.Sign(distance)));
        profileController.MoveRel(axis, distance, profile);
    }

    public void MoveJog(short axis, double velocity)
    {
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Jog, Direction: Math.Sign(velocity), RequiresHomed: false));
        inner.MoveJog(axis, velocity);
    }

    public void GoHome(short axis)
    {
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Home, RequiresHomed: false));
        inner.GoHome(axis);
    }

    private void InvalidateHome(short axis, AxisHomeInvalidationReason reason)
    {
        if (definitions is not null && homeLifecycle is not null)
            homeLifecycle.Invalidate(definitions.GetAxisDefinition(axis).Id, reason);
    }
}
