namespace KwyPecvd.Process.Chambers;

/// <summary>
/// 腔室内一条工艺气路的静态配置。
/// 后续可以继续加入隔离阀、气动阀等设备ID。
/// </summary>
public sealed record ChamberGasLineDefinition
{
    public required string GasName { get; init; }

    public required string MfcId { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            GasName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            MfcId);
    }
}