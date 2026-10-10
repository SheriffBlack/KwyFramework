namespace KwyPecvd.Process.Chambers;

/// <summary>腔体控制器内部的不可变运行状态。</summary>
internal sealed record ChamberRuntimeState
{
    public static IReadOnlyDictionary<string, ChamberGasTarget>
        EmptyGasTargets =>
        new Dictionary<string, ChamberGasTarget>(
            StringComparer.OrdinalIgnoreCase);

    public ChamberState State { get; init; } =
        ChamberState.Offline;

    public DateTimeOffset StateChangedAt { get; init; } =
        DateTimeOffset.UtcNow;

    public string? FaultCode { get; init; }

    public string? FaultMessage { get; init; }

    public IReadOnlyDictionary<string, ChamberGasTarget>
        ActiveGasTargets { get; init; } =
        EmptyGasTargets;
}
