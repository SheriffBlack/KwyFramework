namespace Kwy.Device.Abstractions.Instrument;

/// <summary>
/// 通用仪表测量能力。
/// 品牌驱动可以继续保留自己的 Result 类型，同时把常用测试值转换成该通用结果给流程层使用。
/// </summary>
public interface IMeasurementInstrument : IInstrumentDevice
{
    ValueTask TriggerMeasurementAsync(CancellationToken cancellationToken = default)
        => TriggerAsync(cancellationToken: cancellationToken);

    ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default);

    async ValueTask<InstrumentMeasurementResult> MeasureBySoftwareTriggerAsync(CancellationToken cancellationToken = default)
    {
        await TriggerMeasurementAsync(cancellationToken).ConfigureAwait(false);
        return await ReadMeasurementAsync(cancellationToken).ConfigureAwait(false);
    }
}


/// <summary>
/// Optional formatter that converts raw instrument values to engineering units used by station limits, judgment, UI, charts and persistence.
/// </summary>
public interface IMeasurementDisplayFormatter
{
    InstrumentMeasurementResult ToDisplayMeasurement(InstrumentMeasurementResult result);
}/// <summary>
/// 通用仪表测量结果。
/// </summary>
/// <param name="Values">测量值集合。</param>
/// <param name="RawText">仪表原始响应文本。</param>
public sealed record InstrumentMeasurementResult(IReadOnlyList<InstrumentMeasurementValue> Values, string RawText)
{
    public InstrumentMeasurementValue FirstValue()
    {
        if (Values.Count == 0)
        {
            throw new InvalidOperationException("Instrument measurement contains no values.");
        }

        return Values[0];
    }
}

/// <summary>
/// 单个通用仪表测量值。
/// </summary>
/// <param name="Name">测量项名称，例如 DCR、Ls、Rs。</param>
/// <param name="Value">测量值。</param>
/// <param name="Judgment">判定结果。</param>
/// <param name="RawValue">原始数值文本。</param>
/// <param name="Unit">单位。</param>
public sealed record InstrumentMeasurementValue(
    string Name,
    double Value,
    InstrumentMeasurementJudgment Judgment = InstrumentMeasurementJudgment.Unknown,
    string? RawValue = null,
    string? Unit = null);

public enum InstrumentMeasurementJudgment
{
    Unknown,
    Ok,
    High,
    Low,
    Error
}

