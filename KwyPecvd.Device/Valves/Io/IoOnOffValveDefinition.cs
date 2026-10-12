namespace KwyPecvd.Device.Valves.Io;

/// <summary>
/// 基于数字IO控制的开关阀接线定义。
/// </summary>
public sealed record IoOnOffValveDefinition
{
    public required OnOffValveDefinition Valve { get; init; }

    /// <summary>
    /// 开阀控制DO。单DO和双DO阀均必须配置。
    /// </summary>
    public required string OpenDoId { get; init; }

    /// <summary>
    /// 关阀控制DO。null表示使用单DO控制。
    /// </summary>
    public string? CloseDoId { get; init; }

    /// <summary>开到位反馈DI。</summary>
    public string? OpenDiId { get; init; }

    /// <summary>关到位反馈DI。</summary>
    public string? ClosedDiId { get; init; }

    /// <summary>设备离线反馈DI。</summary>
    public string? OfflineDiId { get; init; }

    /// <summary>
    /// 没有位置反馈时，是否允许使用输出命令作为位置反馈。
    /// </summary>
    public bool UseCommandAsFeedback { get; init; }

    /// <summary>写入DO时使用的资源所有者。</summary>
    public string? OutputOwner { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Valve);

        Valve.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(OpenDoId);

        if (CloseDoId is not null &&
            string.IsNullOrWhiteSpace(CloseDoId))
        {
            throw new ArgumentException(
                "Close DO id cannot be whitespace.",
                nameof(CloseDoId));
        }

        if (OpenDiId is not null &&
            string.IsNullOrWhiteSpace(OpenDiId))
        {
            throw new ArgumentException(
                "Open DI id cannot be whitespace.",
                nameof(OpenDiId));
        }

        if (ClosedDiId is not null &&
            string.IsNullOrWhiteSpace(ClosedDiId))
        {
            throw new ArgumentException(
                "Closed DI id cannot be whitespace.",
                nameof(ClosedDiId));
        }

        if (OfflineDiId is not null &&
            string.IsNullOrWhiteSpace(OfflineDiId))
        {
            throw new ArgumentException(
                "Offline DI id cannot be whitespace.",
                nameof(OfflineDiId));
        }

        var hasPositionFeedback =
            OpenDiId is not null ||
            ClosedDiId is not null;

        if (!hasPositionFeedback &&
            !UseCommandAsFeedback)
        {
            throw new InvalidOperationException(
                $"Valve '{Valve.Id}' must define " +
                "at least one position feedback DI, " +
                "or explicitly enable command feedback.");
        }
    }
}