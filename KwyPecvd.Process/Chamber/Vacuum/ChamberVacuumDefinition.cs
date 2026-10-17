namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>腔体真空子系统的静态硬件绑定和工艺参数。</summary>
public sealed record ChamberVacuumDefinition
{
    public IReadOnlyCollection<ChamberPressureGaugeDefinition> PressureGauges { get; init; } = Array.Empty<ChamberPressureGaugeDefinition>();

    public IReadOnlyCollection<string> ValveIds { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> PumpIds { get; init; } = Array.Empty<string>();

    /// <summary>使用具有 PumpDown 角色的压力计判断抽气完成。</summary>
    public required double ReadyPressure { get; init; }

    public TimeSpan PumpDownTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public void Validate()
    {
        ValidatePressureGauges();
        ValidateIds(ValveIds, nameof(ValveIds), "vacuum valve");
        ValidateIds(PumpIds, nameof(PumpIds), "vacuum pump");

        if (!double.IsFinite(ReadyPressure) ||
            ReadyPressure < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReadyPressure),
                ReadyPressure,
                "Ready pressure must be a finite " +
                "non-negative number.");
        }

        if (PumpDownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PumpDownTimeout),
                PumpDownTimeout,
                "Pump-down timeout must be positive.");
        }
    }

    private void ValidatePressureGauges()
    {
        if (PressureGauges.Count == 0)
        {
            throw new ArgumentException(
                "At least one pressure gauge is required.",
                nameof(PressureGauges));
        }

        foreach (var pressureGauge in PressureGauges)
        {
            ArgumentNullException.ThrowIfNull(pressureGauge);
            pressureGauge.Validate();
        }

        var duplicate = PressureGauges
            .GroupBy(
                item => item.PressureGaugeId,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate pressure gauge id " +
                $"'{duplicate.Key}'.",
                nameof(PressureGauges));
        }

        var pumpDownGaugeCount = PressureGauges.Count(
            item => item.Roles.HasFlag(
                ChamberPressureGaugeRole.PumpDown));

        if (pumpDownGaugeCount != 1)
        {
            throw new ArgumentException(
                "Exactly one pressure gauge must have " +
                "the PumpDown role.",
                nameof(PressureGauges));
        }
    }

    private static void ValidateIds(
        IReadOnlyCollection<string> ids,
        string parameterName,
        string componentName)
    {
        if (ids.Count == 0)
        {
            throw new ArgumentException(
                $"At least one {componentName} is required.",
                parameterName);
        }

        foreach (var id in ids)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
        }

        var duplicate = ids
            .GroupBy(
                id => id,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate {componentName} id " +
                $"'{duplicate.Key}'.",
                parameterName);
        }
    }
}
