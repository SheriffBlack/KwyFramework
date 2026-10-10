namespace KwyPecvd.RT.Requests;

/// <summary>
/// 启动腔体气体准备的临时 HTTP 请求。
/// 后续可由 gRPC 请求模型替代。
/// </summary>
public sealed record StartGasPreparationRequest
{
    public required string ChamberId { get; init; }

    public IReadOnlyCollection<GasTargetRequest> Gases { get; init; } = [];
}

public sealed record GasTargetRequest
{
    public required string GasName { get; init; }

    public double Target { get; init; }

    public double RampSeconds { get; init; }
}