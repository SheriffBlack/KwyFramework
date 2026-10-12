namespace KwyPecvd.RT.Requests;

/// <summary>
/// 启停请求模型
/// </summary>
public sealed record SetTemperatureControllerEnabledRequest
{
    public required string ControllerId { get; init; }

    public bool Enabled { get; init; }
}