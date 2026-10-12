namespace KwyPecvd.Device.PressureGauge;

public sealed record PressureGaugeDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public string Unit { get; init; } = "Pa";

    public double Minimum { get; init; }

    public double Maximum { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);

        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        ArgumentException.ThrowIfNullOrWhiteSpace(Unit);

        if (!double.IsFinite(Minimum) ||
            Minimum < 0)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} minimum " +
                "must be a finite non-negative number.");
        }

        if (!double.IsFinite(Maximum) ||
            Maximum <= Minimum)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} maximum " +
                "must be greater than minimum.");
        }
    }
}