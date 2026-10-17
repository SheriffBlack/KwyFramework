namespace KwyPecvd.Process.Chamber.Vacuum;

public sealed record ChamberPressureGaugeDefinition
{
    public required string PressureGaugeId { get; init; }

    public required ChamberPressureGaugeRole Roles { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PressureGaugeId);

        if (Roles == ChamberPressureGaugeRole.None)
        {
            throw new ArgumentException(
                "At least one pressure gauge role " +
                "must be configured.",
                nameof(Roles));
        }

        const ChamberPressureGaugeRole allRoles =
            ChamberPressureGaugeRole.PumpDown |
            ChamberPressureGaugeRole.ProcessControl |
            ChamberPressureGaugeRole.BasePressure |
            ChamberPressureGaugeRole.Safety;

        if ((Roles & ~allRoles) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Roles),
                Roles,
                "Unknown pressure gauge role.");
        }
    }
}
