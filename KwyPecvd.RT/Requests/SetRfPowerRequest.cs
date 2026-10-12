namespace KwyPecvd.RT.Requests;

/// <summary>
/// RF功率设定请求。
/// </summary>
public sealed record SetRfPowerRequest
{
    public required string PowerSupplyId { get; init; }

    public double Power { get; init; }
}