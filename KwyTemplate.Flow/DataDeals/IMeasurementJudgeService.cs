using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// Software 判定模式下的通用测值判定服务。
/// </summary>
public interface IMeasurementJudgeService
{
    bool IsPass(TestStationModel stationModel, string testName, InstrumentMeasurementValue value);
}
