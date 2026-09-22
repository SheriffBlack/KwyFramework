using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class AxisCoordinateTransformer : IAxisCoordinateTransformer
{
    private readonly IAxisErrorCompensationProvider? compensationProvider;

    public AxisCoordinateTransformer(IAxisErrorCompensationProvider? compensationProvider = null)
        => this.compensationProvider = compensationProvider;

    public double ToMachinePosition(AxisDefinition axis, double calibratedPosition)
    {
        ArgumentNullException.ThrowIfNull(axis);
        double position = calibratedPosition
            + axis.Calibration.MechanicalZeroOffset
            + axis.Calibration.CalibrationZeroOffset;
        if (axis.Calibration.ErrorCompensationTableId is { Length: > 0 } tableId)
        {
            if (compensationProvider is null)
                throw new InvalidOperationException($"Axis '{axis.Id}' requires compensation table '{tableId}', but no provider is configured.");
            position += compensationProvider.GetCompensation(tableId, calibratedPosition);
        }

        return position;
    }

    public double ToCalibratedPosition(AxisDefinition axis, double machinePosition)
    {
        ArgumentNullException.ThrowIfNull(axis);
        double uncompensated = machinePosition
            - axis.Calibration.MechanicalZeroOffset
            - axis.Calibration.CalibrationZeroOffset;
        double position = uncompensated;
        if (axis.Calibration.ErrorCompensationTableId is { Length: > 0 } tableId)
        {
            if (compensationProvider is null)
                throw new InvalidOperationException($"Axis '{axis.Id}' requires compensation table '{tableId}', but no provider is configured.");
            // 误差补偿可能随位置变化，通过固定点迭代反解标定坐标。
            for (int iteration = 0; iteration < 8; iteration++)
                position = uncompensated - compensationProvider.GetCompensation(tableId, position);
        }

        return position;
    }
}

public sealed class AxisBrakeCoordinator : IAxisBrakeCoordinator
{
    private readonly IAxisMotionController controller;
    private readonly IAxisDefinitionProvider definitions;
    private readonly IAxisBrake brake;

    public AxisBrakeCoordinator(IAxisMotionController controller, IAxisDefinitionProvider definitions, IAxisBrake brake)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.brake = brake ?? throw new ArgumentNullException(nameof(brake));
    }

    public async Task PrepareForMotionAsync(short axis, CancellationToken cancellationToken = default)
    {
        AxisSafetyDefinition safety = definitions.GetAxisDefinition(axis).Safety;
        if (!safety.IsVerticalAxis || !safety.HasBrake) return;
        controller.ServoOn(axis);
        await brake.ReleaseAsync(axis, cancellationToken).ConfigureAwait(false);
        if (safety.BrakeReleaseDelay > TimeSpan.Zero)
            await Task.Delay(safety.BrakeReleaseDelay, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteMotionAsync(short axis, CancellationToken cancellationToken = default)
    {
        AxisSafetyDefinition safety = definitions.GetAxisDefinition(axis).Safety;
        if (!safety.IsVerticalAxis || !safety.HasBrake) return;
        if (safety.BrakeEngageDelay > TimeSpan.Zero)
            await Task.Delay(safety.BrakeEngageDelay, cancellationToken).ConfigureAwait(false);
        await brake.EngageAsync(axis, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableAxisAsync(short axis, CancellationToken cancellationToken = default)
    {
        AxisSafetyDefinition safety = definitions.GetAxisDefinition(axis).Safety;
        if (!safety.IsVerticalAxis || !safety.HasBrake)
        {
            controller.ServoOff(axis);
            return;
        }

        if (safety.BrakeDisableStrategy == AxisBrakeDisableStrategy.EngageBeforeServoOff)
        {
            await CompleteMotionAsync(axis, cancellationToken).ConfigureAwait(false);
            controller.ServoOff(axis);
        }
        else
        {
            controller.ServoOff(axis);
            await CompleteMotionAsync(axis, cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class MultiAxisSafetyGuard : IMultiAxisSafetyGuard
{
    private readonly IReadOnlyList<MultiAxisForbiddenZone> zones;

    public MultiAxisSafetyGuard(IEnumerable<MultiAxisForbiddenZone> zones)
    {
        this.zones = zones?.ToArray() ?? throw new ArgumentNullException(nameof(zones));
        foreach (MultiAxisForbiddenZone zone in this.zones)
            zone.Validate();
    }

    public MotionSafetyResult Validate(IReadOnlyDictionary<string, double> targetPositions)
    {
        ArgumentNullException.ThrowIfNull(targetPositions);
        MotionSafetyViolation[] violations = zones
            .Where(zone => zone.Contains(targetPositions))
            .Select(zone => new MotionSafetyViolation("ForbiddenZone", $"Target is inside multi-axis forbidden zone '{zone.Id}'."))
            .ToArray();
        return violations.Length == 0 ? MotionSafetyResult.Allowed : new(violations);
    }
}

public sealed class RotaryAxisPathPlanner : IRotaryAxisPathPlanner
{
    public double ResolveTarget(AxisDefinition axis, double currentPosition, double requestedPosition)
    {
        ArgumentNullException.ThrowIfNull(axis);
        RotaryAxisDefinition rotary = axis.Rotary
            ?? throw new ArgumentException($"Axis '{axis.Id}' is not configured as a rotary axis.", nameof(axis));
        if (rotary.PositionMode == RotaryPositionMode.Accumulated)
            return ValidateCandidate(rotary, requestedPosition);

        double normalized = requestedPosition - Math.Floor(requestedPosition / rotary.Period) * rotary.Period;
        if (rotary.IsForbidden(normalized))
            throw new InvalidOperationException($"Requested angle is forbidden for axis '{axis.Id}'.");

        double minimumTurn = rotary.MinimumAccumulatedAngle is { } minimum
            ? Math.Ceiling((minimum - normalized) / rotary.Period)
            : double.NegativeInfinity;
        double maximumTurn = rotary.MaximumAccumulatedAngle is { } maximum
            ? Math.Floor((maximum - normalized) / rotary.Period)
            : double.PositiveInfinity;
        if (minimumTurn > maximumTurn)
            throw new InvalidOperationException($"No legal accumulated-angle target exists for axis '{axis.Id}'.");

        double turn = rotary.PreferredDirection switch
        {
            > 0 => Math.Ceiling((currentPosition - normalized) / rotary.Period),
            < 0 => Math.Floor((currentPosition - normalized) / rotary.Period),
            _ => Math.Round((currentPosition - normalized) / rotary.Period, MidpointRounding.AwayFromZero)
        };
        turn = Math.Clamp(turn, minimumTurn, maximumTurn);
        double candidate = normalized + turn * rotary.Period;
        if (IsAllowed(rotary, candidate)
            && (rotary.PreferredDirection <= 0 || candidate >= currentPosition)
            && (rotary.PreferredDirection >= 0 || candidate <= currentPosition))
            return candidate;

        throw new InvalidOperationException($"No legal rotary path target exists for axis '{axis.Id}'.");
    }

    private static double ValidateCandidate(RotaryAxisDefinition rotary, double candidate)
        => IsAllowed(rotary, candidate)
            ? candidate
            : throw new InvalidOperationException("Requested rotary target violates accumulated or forbidden-angle limits.");

    private static bool IsAllowed(RotaryAxisDefinition rotary, double candidate)
        => (rotary.MinimumAccumulatedAngle is not { } minimum || candidate >= minimum)
            && (rotary.MaximumAccumulatedAngle is not { } maximum || candidate <= maximum)
            && !rotary.IsForbidden(candidate);
}