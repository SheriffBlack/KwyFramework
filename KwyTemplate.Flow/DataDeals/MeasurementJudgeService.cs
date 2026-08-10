using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// 默认测值判定：优先使用工站测试项上下限；没有上下限时沿用仪表自身 Judgment。
/// </summary>
public sealed class MeasurementJudgeService : IMeasurementJudgeService
{
    private const double LimitComparisonTolerance = 1e-8;
    public static MeasurementJudgeService Instance { get; } = new();

    private MeasurementJudgeService()
    {
    }

    public bool IsPass(TestStationModel stationModel, string testName, InstrumentMeasurementValue value)
    {
        ArgumentNullException.ThrowIfNull(stationModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        if (stationModel.TestLimits.TryGetValue(testName, out StationMeasurementLimit? limit)
            && (limit.LowerLimit.HasValue || limit.UpperLimit.HasValue))
        {
            return IsInRange(testName, value, limit);
        }

        // 兼容未下发上下限的老流程：驱动能给出 OK 就信驱动；Unknown 视为不阻断。
        return value.Judgment is InstrumentMeasurementJudgment.Ok or InstrumentMeasurementJudgment.Unknown;
    }

    private static bool IsInRange(string testName, InstrumentMeasurementValue value, StationMeasurementLimit limit)
    {
        double measuredValue = ConvertMeasurementToComparableValue(testName, value);
        double? lowerLimit = limit.LowerLimit.HasValue ? ConvertLimitToComparableValue(testName, limit.LowerLimit.Value, limit.Unit) : null;
        double? upperLimit = limit.UpperLimit.HasValue ? ConvertLimitToComparableValue(testName, limit.UpperLimit.Value, limit.Unit) : null;

        return (!lowerLimit.HasValue || measuredValue >= lowerLimit.Value - LimitComparisonTolerance)
            && (!upperLimit.HasValue || measuredValue <= upperLimit.Value + LimitComparisonTolerance);
    }

    private static double ConvertMeasurementToComparableValue(string testName, InstrumentMeasurementValue value)
    {
        if (!string.IsNullOrWhiteSpace(value.Unit))
        {
            return MeasurementUnitConverter.ToBaseUnit(value.Value, testName, value.Unit);
        }

        // HIOKI LCR measurement responses are parsed as base SI values: H, Ω, F, degree.
        return value.Value;
    }

    private static double ConvertLimitToComparableValue(string testName, double value, string? unit)
        => MeasurementUnitConverter.ToBaseUnit(value, testName, unit);
}
