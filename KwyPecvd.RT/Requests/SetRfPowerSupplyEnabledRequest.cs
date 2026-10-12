namespace KwyPecvd.RT.Requests;

/// <summary>
/// RF输出启停请求。
/// </summary>
public sealed record SetRfPowerSupplyEnabledRequest
{
    public required string PowerSupplyId { get; init; }

    public bool Enabled { get; init; }
}