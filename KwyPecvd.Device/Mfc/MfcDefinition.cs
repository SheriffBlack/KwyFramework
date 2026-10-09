namespace KwyPecvd.Device.Mfc;

/// <summary>
/// 描述静态信息
/// </summary>
public sealed record MfcDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public string GasName { get; init; } = string.Empty;

    public string Unit { get; init; } = "sccm";

    public required double FullScale { get; init; }

    public double Tolerance { get; init; } = 2;

    public TimeSpan ToleranceDelay { get; init; }
        = TimeSpan.FromSeconds(3);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        ArgumentException.ThrowIfNullOrWhiteSpace(Unit);

        if (!double.IsFinite(FullScale) || FullScale <= 0)
            throw new InvalidOperationException(
                $"MFC {Id} full scale must be a finite number greater than zero.");

        if (!double.IsFinite(Tolerance) || Tolerance < 0)
            throw new InvalidOperationException(
                $"MFC {Id} tolerance must be a finite non-negative number.");

        if (ToleranceDelay < TimeSpan.Zero)
            throw new InvalidOperationException(
                $"MFC {Id} tolerance delay cannot be negative.");
    }
}
