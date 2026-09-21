namespace Kwy.Device.Abstractions.Motion;

public interface IAxisErrorCompensationProvider
{
    double GetCompensation(string tableId, double engineeringPosition);
}

public interface IAxisCoordinateTransformer
{
    double ToMachinePosition(AxisDefinition axis, double calibratedPosition);
    double ToCalibratedPosition(AxisDefinition axis, double machinePosition);
}

public interface IAxisBrake
{
    Task ReleaseAsync(short axis, CancellationToken cancellationToken = default);
    Task EngageAsync(short axis, CancellationToken cancellationToken = default);
}

public interface IAxisBrakeCoordinator
{
    Task PrepareForMotionAsync(short axis, CancellationToken cancellationToken = default);
    Task CompleteMotionAsync(short axis, CancellationToken cancellationToken = default);
    Task DisableAxisAsync(short axis, CancellationToken cancellationToken = default);
}

public sealed record AxisPositionCoordinate(short Axis, double Position);

public sealed record MultiAxisForbiddenZone(
    string Id,
    IReadOnlyDictionary<short, AxisForbiddenRange> Bounds)
{
    public bool Contains(IReadOnlyDictionary<short, double> positions)
        => Bounds.Count > 0 && Bounds.All(item =>
            positions.TryGetValue(item.Key, out double position) && item.Value.Contains(position));

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Bounds);
        if (Bounds.Count == 0)
            throw new ArgumentException("A multi-axis forbidden zone must contain at least one axis bound.", nameof(Bounds));
        foreach ((short axis, AxisForbiddenRange range) in Bounds)
        {
            if (axis < 1) throw new ArgumentOutOfRangeException(nameof(Bounds), axis, "Axis must be greater than or equal to 1.");
            range.Validate();
        }
    }
}

public interface IMultiAxisSafetyGuard
{
    MotionSafetyResult Validate(IReadOnlyDictionary<short, double> targetPositions);
}

public interface IRotaryAxisPathPlanner
{
    double ResolveTarget(AxisDefinition axis, double currentPosition, double requestedPosition);
}
