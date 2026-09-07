namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesStandardSampleRequest(
    MesRequestContext Context,
    string WorkOrderNo,
    string? SampleCode = null);

public sealed record MesStandardSampleSetup(
    string WorkOrderNo,
    string? SampleCode,
    IReadOnlyList<MesMeasurementLimit> MeasurementLimits,
    MesParameterBag Parameters,
    MesExternalDataSource? DataSource = null);

public sealed record MesStandardSampleCheckSaveRequest(
    MesRequestContext Context,
    string WorkOrderNo,
    string SampleCode,
    bool Passed,
    DateTimeOffset Time,
    IReadOnlyList<MesMeasurementResult> Measurements);