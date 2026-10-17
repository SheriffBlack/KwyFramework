namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 腔体热子系统的静态硬件绑定和公共工艺参数。
/// </summary>
public sealed record ChamberThermalDefinition
{
    public IReadOnlyCollection<ChamberThermalZoneDefinition> Zones { get; init; } = Array.Empty<ChamberThermalZoneDefinition>();

    /// <summary>
    /// 所有目标温区进入允许范围后，
    /// 还需要连续稳定的时间。
    /// </summary>
    public TimeSpan StableDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从启动加热到温度准备完成的最长时间。
    /// </summary>
    public TimeSpan HeatingTimeout { get; init; } = TimeSpan.FromMinutes(30);

    public void Validate()
    {
        if (Zones.Count == 0)
        {
            throw new ArgumentException(
                "At least one thermal zone is required.",
                nameof(Zones));
        }

        foreach (var zone in Zones)
        {
            ArgumentNullException.ThrowIfNull(zone);
            zone.Validate();
        }

        var duplicateZone =
            Zones
                .GroupBy(
                    zone => zone.ZoneId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group => group.Count() > 1);

        if (duplicateZone is not null)
        {
            throw new ArgumentException(
                $"Duplicate thermal zone id " +
                $"'{duplicateZone.Key}'.",
                nameof(Zones));
        }

        var duplicateController =
            Zones
                .GroupBy(
                    zone => zone.ControllerId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group => group.Count() > 1);

        if (duplicateController is not null)
        {
            throw new ArgumentException(
                $"Temperature controller " +
                $"'{duplicateController.Key}' is assigned " +
                "to more than one thermal zone.",
                nameof(Zones));
        }

        if (StableDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StableDuration));
        }

        if (HeatingTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HeatingTimeout));
        }
    }
}