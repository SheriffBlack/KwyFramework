namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesWorkOrderRequest(
    MesRequestContext Context,
    string WorkOrderNo);

public sealed record MesWorkOrderSetup(
    string WorkOrderNo,
    string? ProductNo,
    string? ProductName,
    string? RecipeName,
    string? RecipeRevision,
    MesParameterBag Parameters,
    IReadOnlyList<MesMeasurementLimit> MeasurementLimits,
    MesExternalDataSource? DataSource = null,
    string? EquipmentType = null,
    IReadOnlyList<MesWorkOrderInstrumentSetup>? InstrumentSetups = null,
    MesWorkOrderMaterialRequirements? MaterialRequirements = null,
    MesWorkOrderTapeSetup? TapeSetup = null,
    int? StandardSampleCheckInterval = null);

public sealed record MesWorkOrderInstrumentSetup(
    string ParameterId,
    string DisplayName,
    double? LowerLimit,
    double? UpperLimit,
    string? Unit,
    string? Range);

public sealed record MesWorkOrderMaterialRequirements(
    string? TablePaperMatNo,
    string? TopCoverMatNo,
    string? ReelMatNo);

public sealed record MesWorkOrderTapeSetup(
    int? BeforeSpaceQty,
    int? PackageQty,
    int? AfterSpaceQty,
    int? SampleQty,
    int? BlankQty,
    int? BackNoFilmQty);
