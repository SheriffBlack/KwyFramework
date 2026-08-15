using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 工站数据采集能力。实现类负责读取一次测试数据，并写入 TestValues、TestJudges。
/// </summary>
public interface IStationDataDeal
{
    Task<IStationDataCapture> CaptureAsync(CancellationToken cancellationToken = default);

    void ApplyCapture(IStationDataCapture capture, bool triggerResult, TestStationModel stationModel);

    async Task CollectAsync(bool triggerResult, TestStationModel stationModel, CancellationToken cancellationToken = default)
    {
        IStationDataCapture capture = await CaptureAsync(cancellationToken).ConfigureAwait(false);
        ApplyCapture(capture, triggerResult, stationModel);
    }
}

public interface IStationDataCapture;
