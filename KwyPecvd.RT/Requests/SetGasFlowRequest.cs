namespace KwyPecvd.RT.Requests;

/// <summary>
/// 临时HTTP接口使用的气体流量设置请求。
/// 后续由gRPC生成的请求模型替换。
/// </summary>
public sealed record SetGasFlowRequest
{
    public required string ChamberId { get; init; }

    public required string GasName { get; init; }

    public double Target { get; init; }

    public double RampSeconds { get; init; }
}