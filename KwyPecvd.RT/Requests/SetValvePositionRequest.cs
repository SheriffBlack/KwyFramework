using KwyPecvd.Device.OnOffValve;

namespace KwyPecvd.RT.Requests;

/// <summary>
/// 临时阀门调试请求，后续正常工艺命令由Process层提供。
/// </summary>
public sealed record SetValvePositionRequest
{
    public required string ValveId { get; init; }

    public required OnOffValvePosition Position { get; init; }
}