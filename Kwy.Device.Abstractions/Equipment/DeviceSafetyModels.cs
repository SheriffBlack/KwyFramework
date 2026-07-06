namespace Kwy.Device.Abstractions.Equipment;

public sealed record DeviceSafetyViolation(string Code, string Message);

public sealed record DeviceSafetyResult(IReadOnlyList<DeviceSafetyViolation> Violations)
{
    public static DeviceSafetyResult Allowed { get; } = new(Array.Empty<DeviceSafetyViolation>());

    public bool IsAllowed => Violations.Count == 0;
}

public sealed class DeviceSafetyException : InvalidOperationException
{
    public DeviceSafetyException(IReadOnlyList<DeviceSafetyViolation> violations)
        : base(string.Join("; ", violations.Select(item => item.Message)))
    {
        Violations = violations;
    }

    public IReadOnlyList<DeviceSafetyViolation> Violations { get; }
}
