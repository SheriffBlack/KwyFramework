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

    public MotionSafetyGuard(IMotionCard card, IMotionStateProvider stateProvider, MotionSafetyOptions options)
    {
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        this.stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
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

    public SafeAxisMotionController(
        IAxisMotionController inner,
        IMotionProfileController profileController,
        IAxisStatusReader statusReader,
        IMotionSafetyGuard safetyGuard)
    {
        this.inner = inner;
        this.profileController = profileController;
        this.statusReader = statusReader;
        this.safetyGuard = safetyGuard;
    }

    public void ServoOn(short axis) => inner.ServoOn(axis);
    public void ServoOff(short axis) => inner.ServoOff(axis);
    public void ClearError(short axis) => inner.ClearError(axis);
    public void Stop(short axis) => inner.Stop(axis);
    public void Abort(short axis) => inner.Abort(axis);
    public void SetSoftLimit(short axis, double positive, double negative) => inner.SetSoftLimit(axis, positive, negative);

    public void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5)
    {
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Absolute, position, Math.Sign(position - statusReader.GetPosition(axis))));
        inner.MoveAbs(axis, position, velocity, acc, dec);
    }

    public void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5)
    {
        double target = statusReader.GetPosition(axis) + distance;
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Relative, target, Math.Sign(distance)));
        inner.MoveRel(axis, distance, velocity, acc, dec);
    }

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
}
