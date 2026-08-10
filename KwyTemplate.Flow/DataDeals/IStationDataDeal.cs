using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 工站数据采集能力。实现类负责读取一次测试数据，并写入 TestValues、TestJudges。
/// </summary>
public interface IStationDataDeal
{
    Task CollectAsync(bool triggerResult, TestStationModel stationModel, CancellationToken cancellationToken = default);
}