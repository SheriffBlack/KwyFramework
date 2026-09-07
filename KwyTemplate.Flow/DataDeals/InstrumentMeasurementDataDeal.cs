using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 閫氱敤浠〃璇绘暟澶勭悊鍣ㄣ€?/// Hardware 妯″紡涓?OK/NG 浣跨敤 IO 鍒ゅ畾缁撴灉锛汼oftware 妯″紡涓嬫墠浣跨敤浠〃 Judgment銆?/// </summary>
public class InstrumentMeasurementDataDeal : IStationDataDeal, IStationInstrumentOperation
{
    private readonly IMeasurementInstrument? meter;
    private readonly IMeasurementJudgeService judgeService;

    public InstrumentMeasurementDataDeal(
        string testName,
        IMeasurementInstrument? meter = null,
        int valueIndex = 0,
        IMeasurementJudgeService? judgeService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        if (valueIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueIndex), valueIndex, "Value index cannot be negative.");
        }

        TestName = testName;
        this.meter = meter;
        ValueIndex = valueIndex;
        this.judgeService = judgeService ?? MeasurementJudgeService.Instance;
    }

    public string TestName { get; }

    public int ValueIndex { get; }

    public async Task TriggerAsync(CancellationToken cancellationToken = default)
    {
        if (meter is { IsConnected: true })
        {
            await meter.TriggerMeasurementAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default)
    {
        if (meter == null || !meter.IsConnected)
        {
            throw new InvalidOperationException($"{TestName} instrument is not connected.");
        }

        return await meter.ReadMeasurementAsync(cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask<InstrumentMeasurementResult> ReadDisplayMeasurementAsync(CancellationToken cancellationToken = default)
    {
        InstrumentMeasurementResult result = await ReadMeasurementAsync(cancellationToken).ConfigureAwait(false);
        return meter is IMeasurementDisplayFormatter formatter
            ? formatter.ToDisplayMeasurement(result)
            : result;
    }

    public async ValueTask<InstrumentMeasurementResult> MeasureBySoftwareTriggerAsync(CancellationToken cancellationToken = default)
    {
        if (meter == null || !meter.IsConnected)
        {
            throw new InvalidOperationException($"{TestName} instrument is not connected.");
        }

        InstrumentMeasurementResult result = await meter.MeasureBySoftwareTriggerAsync(cancellationToken).ConfigureAwait(false);
        return meter is IMeasurementDisplayFormatter formatter
            ? formatter.ToDisplayMeasurement(result)
            : result;
    }

    public async Task<IStationDataCapture> CaptureAsync(CancellationToken cancellationToken = default)
        => new MeasurementCapture(await ReadMeasurementAsync(cancellationToken).ConfigureAwait(false));

    public void ApplyCapture(IStationDataCapture capture, bool triggerResult, TestStationModel stationModel)
    {
        ArgumentNullException.ThrowIfNull(stationModel);
        if (capture is not MeasurementCapture measurementCapture)
        {
            throw new ArgumentException("Measurement capture type does not match the data deal.", nameof(capture));
        }

        RefreshStationLimitFromInstrumentConfig(stationModel);
        InstrumentMeasurementResult result = ToEngineeringMeasurement(
            measurementCapture.Result);
        InstrumentMeasurementValue value = GetValue(result, TestName, ValueIndex);
        stationModel.TestValues[TestName] = value.Value;
        stationModel.TestJudges[TestName] = stationModel.StationIo.ResultSource == StationResultSource.Hardware
            ? triggerResult
            : judgeService.IsPass(stationModel, TestName, value);
    }

    private sealed record MeasurementCapture(InstrumentMeasurementResult Result) : IStationDataCapture;

    private void RefreshStationLimitFromInstrumentConfig(TestStationModel stationModel)
    {
        if (meter is IMeasurementLimitSetProvider limitSetProvider
            && limitSetProvider.TryGetMeasurementLimits(out IReadOnlyDictionary<string, InstrumentMeasurementLimit>? limits)
            && limits.TryGetValue(TestName, out InstrumentMeasurementLimit? namedLimit))
        {
            stationModel.SetTestLimit(TestName, namedLimit.LowerLimit, namedLimit.UpperLimit, namedLimit.Unit);
            return;
        }

        if (meter is not IMeasurementLimitProvider limitProvider
            || !limitProvider.TryGetMeasurementLimit(out InstrumentMeasurementLimit? sharedLimit))
        {
            return;
        }

        stationModel.SetTestLimit(TestName, sharedLimit.LowerLimit, sharedLimit.UpperLimit, sharedLimit.Unit);
    }
    private InstrumentMeasurementResult ToEngineeringMeasurement(InstrumentMeasurementResult result)
        => meter is IMeasurementDisplayFormatter formatter
            ? formatter.ToDisplayMeasurement(result)
            : result;

    private static InstrumentMeasurementValue GetValue(InstrumentMeasurementResult result, string testName, int valueIndex)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Values.Count <= valueIndex)
        {
            throw new InvalidOperationException($"{testName} instrument returned insufficient values. ValueIndex={valueIndex}, Count={result.Values.Count}.");
        }

        return result.Values[valueIndex];
    }
}
