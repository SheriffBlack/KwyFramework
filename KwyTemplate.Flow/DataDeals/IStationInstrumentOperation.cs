using Kwy.Device.Abstractions.Instrument;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 宸ョ珯鍐呬华琛ㄦ搷浣滆兘鍔涳紝鐢ㄤ簬鏍囧噯浠躲€佺‘璁や欢銆佹牎鍑嗙瓑闇€瑕佹寜宸ョ珯瀹氫綅浠〃鐨勭壒娈婃祦绋嬨€?/// </summary>
public interface IStationInstrumentOperation
{
    string TestName { get; }

    Task TriggerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// return 鈥滃師鍊?+ 鍑€鍊尖€?
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
