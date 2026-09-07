using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// IO 结果采集器。适用于没有独立仪表读数、只需要把硬件 OK/NG 映射到测试值和判定的工位。
/// PassValue、FailValue主要服务于这种“没有真实仪表数值，只有 OK/NG 结果”的工位。比如极性工位
/// </summary>
public sealed class StationIoResultDataDeal : IStationDataDeal
{
    public StationIoResultDataDeal(string testName, double passValue = 1, double failValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        TestName = testName;
        PassValue = passValue;
        FailValue = failValue;
    }

    public string TestName { get; }

    public double PassValue { get; }

    public double FailValue { get; }

    public Task<IStationDataCapture> CaptureAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IStationDataCapture>(new IoResultCapture());

    public void ApplyCapture(IStationDataCapture capture, bool triggerResult, TestStationModel stationModel)
    {
        ArgumentNullException.ThrowIfNull(stationModel);
        if (capture is not IoResultCapture)
        {
            throw new ArgumentException("Measurement capture type does not match the data deal.", nameof(capture));
        }

        stationModel.TestValues[TestName] = triggerResult ? PassValue : FailValue;
        stationModel.TestJudges[TestName] = triggerResult;
    }

    private sealed record IoResultCapture : IStationDataCapture;
}
