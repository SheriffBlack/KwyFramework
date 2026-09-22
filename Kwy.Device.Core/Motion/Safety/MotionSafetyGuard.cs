using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class MotionSafetyOptions
{
    public TimeSpan MaximumSnapshotAge { get; set; } = TimeSpan.FromMilliseconds(500);

    public bool RequireServoEnabled { get; set; } = true;

    public bool RequireHomedForPositioning { get; set; } = true;

    public IDictionary<short, (double Negative, double Positive)> SoftwareLimits { get; }
        = new Dictionary<short, (double Negative, double Positive)>();

    public IList<Func<MotionRequest, MotionSafetyViolation?>> AdditionalRules { get; }
        = new List<Func<MotionRequest, MotionSafetyViolation?>>();
}

public sealed class MotionSafetyGuard : IMotionSafetyGuard
{
    private readonly IMotionCard card;
    private readonly IMotionStateProvider stateProvider;
    private readonly MotionSafetyOptions options;
    private readonly IAxisDefinitionProvider? axisDefinitions;
    private readonly IAxisHomeLifecycle? homeLifecycle;

    public MotionSafetyGuard(IMotionCard card, IMotionStateProvider stateProvider, MotionSafetyOptions options, IAxisHomeLifecycle? homeLifecycle = null)
    {
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        this.stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        axisDefinitions = card as IAxisDefinitionProvider;
        this.homeLifecycle = homeLifecycle;
    }

    public MotionSafetyResult Validate(MotionRequest request)
    {
        List<MotionSafetyViolation>? violations = null;

        if (!card.IsConnected)
        {
            return new(new[] { new MotionSafetyViolation("NotConnected", "Motion card is not connected.") });
        }

        MotionAxisSnapshot snapshot;
        try
        {
            snapshot = stateProvider.GetAxisSnapshot(request.Axis);
        }
        catch (KeyNotFoundException)
        {
            return new(new[] { new MotionSafetyViolation("NoSnapshot", $"Axis {request.Axis} has no state snapshot.") });
        }

        if (DateTimeOffset.Now - snapshot.Timestamp > options.MaximumSnapshotAge)
        {
            AddViolation(ref violations, "StaleSnapshot", $"Axis {request.Axis} state snapshot is stale.");
        }

        if (snapshot.IsAlarm)
        {
            AddViolation(ref violations, "AxisAlarm", $"Axis {request.Axis} is in alarm state.");
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
            MotionSafetyViolation? violation = rule(request);
            if (violation is not null)
            {
                (violations ??= new List<MotionSafetyViolation>()).Add(violation);
            }
        }

        return violations is null ? MotionSafetyResult.Allowed : new(violations);
    }

    public void ValidateAndThrow(MotionRequest request)
    {
        MotionSafetyResult result = Validate(request);
        if (!result.IsAllowed)
        {
            throw new MotionSafetyException(result.Violations);
        }
    }

    private static void AddViolation(ref List<MotionSafetyViolation>? violations, string code, string message)
        => (violations ??= new List<MotionSafetyViolation>()).Add(new(code, message));
}

public sealed class SafeAxisMotionController : ISafeAxisMotionController
{
    private readonly IAxisMotionController inner;
    private readonly IMotionProfileController profileController;
    private readonly IAxisStatusReader statusReader;
    private readonly IMotionSafetyGuard safetyGuard;
    private readonly IAxisDefinitionProvider? definitions;
    private readonly IAxisHomeLifecycle? homeLifecycle;

    public SafeAxisMotionController(
        IAxisMotionController inner,
        IMotionProfileController profileController,
        IAxisStatusReader statusReader,
        IMotionSafetyGuard safetyGuard,
        IAxisDefinitionProvider? definitions = null,
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
