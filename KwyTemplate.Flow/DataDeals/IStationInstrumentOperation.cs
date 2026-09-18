using Kwy.Device.Abstractions.Instrument;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 工站内仪表操作能力，用于标准件、确认件、校准等需要按工站定位仪表的特殊流程。
/// </summary>
public interface IStationInstrumentOperation
{
    string TestName { get; }

    Task TriggerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// return “原值 + 净值”
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default);

    ValueTask<InstrumentMeasurementResult> ReadDisplayMeasurementAsync(CancellationToken cancellationToken = default)
        => ReadMeasurementAsync(cancellationToken);

    async ValueTask<InstrumentMeasurementResult> MeasureBySoftwareTriggerAsync(CancellationToken cancellationToken = default)
    {
        await TriggerAsync(cancellationToken).ConfigureAwait(false);
        return await ReadDisplayMeasurementAsync(cancellationToken).ConfigureAwait(false);
    }
}
