using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>GEM Trace 采集状态。</summary>
/// <remarks>Host 通常通过 S2F23 配置 Trace，Equipment 通过 S6F1 上报采样数据。</remarks>
public enum GemTraceState
{
    /// <summary>Trace 采集关闭。</summary>
    Disabled,
    /// <summary>订阅配置完成，尚未开始采集。</summary>
    Enabled,
    /// <summary>正在采集并向 Host 上报 Trace 数据。</summary>
    Active,
    /// <summary>采集计划正常结束。</summary>
    Completed,
    /// <summary>采集被 Host 取消。</summary>
    Cancelled
}

public sealed record GemTraceDefinition(
    uint TraceId,
    TimeSpan SampleInterval,
    uint TotalSamples,
    IReadOnlyList<GemVid> VariableIds,
    GemTraceState State = GemTraceState.Enabled);

public sealed record GemTraceSample(
    uint TraceId,
    uint SampleNumber,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<GemVid, Item> Values);

public sealed record GemTraceRunResult(
    uint TraceId,
    uint SamplesReported,
    GemTraceState State,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
