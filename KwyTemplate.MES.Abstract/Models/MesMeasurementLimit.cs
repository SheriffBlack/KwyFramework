namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesMeasurementLimit(
    string ParameterId,
    string DisplayName,
    double? LowerLimit,
    double? UpperLimit,
    double? StandardValue,
    string? Unit = null,
    string? SerialNo = null,
    string? MeterType = null,
    string? ItemName = null,
    string? Frequency = null,
    string? FrequencyUnit = null);

public sealed record MesMeasurementResult(
    string ParameterId,
    string DisplayName,
    double Value,
    bool Passed,
    double? LowerLimit = null,
    double? UpperLimit = null,
    double? StandardValue = null,
    string? Unit = null,
    string? SampleId = null,
    string? MeterType = null,
    string? MeterSerialNo = null,
    string? ItemName = null,
    string? Frequency = null,
    string? FrequencyUnit = null);