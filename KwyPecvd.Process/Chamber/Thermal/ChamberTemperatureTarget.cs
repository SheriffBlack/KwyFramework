namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 一次腔体温度准备所要求的温区目标。
/// </summary>
public sealed record ChamberTemperatureTarget
{
    public required string ZoneId { get; init; }

    public required double Temperature { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ZoneId);

        if (!double.IsFinite(Temperature))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Temperature),
                Temperature,
                "Target temperature must be finite.");
        }
    }
}