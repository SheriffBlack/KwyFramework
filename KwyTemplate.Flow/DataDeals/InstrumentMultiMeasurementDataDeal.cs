using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 涓€鍙颁华琛ㄤ竴娆¤繑鍥炲涓祴閲忓€兼椂浣跨敤鐨勯€氱敤閲囬泦鍣ㄣ€?/// 渚嬪 HIOKI LCR 涓€娆¤繑鍥?Ls銆丷s锛屽氨璇诲彇涓€娆′华琛ㄥ悗鍒嗗埆鍐欏叆澶氫釜娴嬭瘯椤广€?/// </summary>
public sealed class InstrumentMultiMeasurementDataDeal : IStationDataDeal, IStationInstrumentOperation
{
    private readonly IMeasurementInstrument? meter;
    private readonly IReadOnlyList<MeasurementValueMapping>? mappings;
    private readonly IMeasurementJudgeService judgeService;

    public InstrumentMultiMeasurementDataDeal(
        IMeasurementInstrument? meter,
        IReadOnlyList<MeasurementValueMapping>? mappings = null,
        IMeasurementJudgeService? judgeService = null)
    {
        this.meter = meter;
        this.mappings = mappings;
        this.judgeService = judgeService ?? MeasurementJudgeService.Instance;
    }

    public string TestName
    {
        get
        {
            IReadOnlyList<MeasurementValueMapping> activeMappings = mappings ?? InstrumentMeasurementNameHelper.CreateMappings(meter);
            return activeMappings.Count == 0 ? string.Empty : string.Join("/", activeMappings.Select(static item => item.TestName));
        }
    }

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
            throw new InvalidOperationException("Multi-value instrument is not connected.");
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
            throw new InvalidOperationException("Multi-value instrument is not connected.");
        }

        InstrumentMeasurementResult result = await meter.MeasureBySoftwareTriggerAsync(cancellationToken).ConfigureAwait(false);
        return meter is IMeasurementDisplayFormatter formatter
            ? formatter.ToDisplayMeasurement(result)
            : result;
    }

    public async Task CollectAsync(bool triggerResult, TestStationModel stationModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stationModel);

        IReadOnlyList<MeasurementValueMapping> activeMappings = mappings ?? InstrumentMeasurementNameHelper.CreateMappings(meter);
        SyncStationTestNames(stationModel, activeMappings);
        RefreshStationLimitsFromInstrumentConfig(stationModel, activeMappings);
        InstrumentMeasurementResult result = ToEngineeringMeasurement(
            await ReadMeasurementAsync(cancellationToken).ConfigureAwait(false));
        foreach (MeasurementValueMapping mapping in activeMappings)
        {
            InstrumentMeasurementValue value = GetValue(result, mapping);
            stationModel.TestValues[mapping.TestName] = value.Value;
            stationModel.TestJudges[mapping.TestName] = stationModel.StationIo.ResultSource == StationResultSource.Hardware
                ? triggerResult
                : judgeService.IsPass(stationModel, mapping.TestName, value);
        }
    }

    private static void SyncStationTestNames(TestStationModel stationModel, IReadOnlyList<MeasurementValueMapping> activeMappings)
    {
        if (!stationModel.UseInstrumentConfigTestNames)
        {
            return;
        }

        string[] testNames = activeMappings
            .Select(static mapping => mapping.TestName)
            .Where(static testName => !string.IsNullOrWhiteSpace(testName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (testNames.Length == 0
            || stationModel.OrderedTestNames.SequenceEqual(testNames, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        stationModel.OrderedTestNames = testNames.ToList();
    }
    private void RefreshStationLimitsFromInstrumentConfig(TestStationModel stationModel, IReadOnlyList<MeasurementValueMapping> activeMappings)
    {
        if (meter is not IMeasurementLimitSetProvider limitSetProvider
            || !limitSetProvider.TryGetMeasurementLimits(out IReadOnlyDictionary<string, InstrumentMeasurementLimit>? limits))
        {
            return;
        }

        foreach (MeasurementValueMapping mapping in activeMappings)
        {
            if (limits.TryGetValue(mapping.TestName, out InstrumentMeasurementLimit? limit))
            {
                stationModel.SetTestLimit(mapping.TestName, limit.LowerLimit, limit.UpperLimit, limit.Unit);
            }
        }
    }

    private InstrumentMeasurementResult ToEngineeringMeasurement(InstrumentMeasurementResult result)
        => meter is IMeasurementDisplayFormatter formatter
            ? formatter.ToDisplayMeasurement(result)
            : result;

    private static InstrumentMeasurementValue GetValue(InstrumentMeasurementResult result, MeasurementValueMapping mapping)
    {
        if (result.Values.Count <= mapping.ValueIndex)
        {
            throw new InvalidOperationException($"{mapping.TestName} instrument returned insufficient values. ValueIndex={mapping.ValueIndex}, Count={result.Values.Count}.");
        }

        return result.Values[mapping.ValueIndex];
    }
}

public sealed record MeasurementValueMapping(string TestName, int ValueIndex);
