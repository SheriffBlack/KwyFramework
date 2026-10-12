namespace KwyPecvd.RT.Requests;

/// <summary>
/// 温度请求模型
/// </summary>
public sealed record SetTemperatureTargetRequest
{
    public required string ControllerId { get; init; }

    public double Target { get; init; }
}