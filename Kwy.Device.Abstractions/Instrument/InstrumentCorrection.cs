namespace Kwy.Device.Abstractions.Instrument;

/// <summary>
/// 仪表校正能力接口。
/// 仅由支持开路、短路、负载校正的仪表实现，例如部分 HIOKI LCR、Keysight LCR。
/// 普通测量仪表不需要实现该接口，避免在 InstrumentBase 中堆空方法。
/// </summary>
public interface IInstrumentCorrection : IInstrumentDevice
{
    /// <summary>
    /// 当前仪表支持的负载校正类型。
    /// 不同品牌或型号的模式集合可能不同，UI 应从这里读取，而不是写死具体仪表的模式。
    /// </summary>
    IReadOnlyList<string> SupportedLoadCorrectionTypes { get; }

    /// <summary>
    /// 当前仪表建议默认使用的负载校正类型。
    /// 通常来自仪表参数配置；为空时 UI 可回退到支持列表的第一项。
    /// </summary>
    string DefaultLoadCorrectionType { get; }
    /// <summary>
    /// 执行开路校正。
    /// 通常要求夹具处于开路状态，由仪表采集开路补偿数据。
    /// </summary>
    ValueTask ExecuteOpenCorrectionAsync(InstrumentCorrectionConditionRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取开路校正后的仪表回传值。
    /// </summary>
    ValueTask<InstrumentCorrectionData> ReadOpenCorrectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行短路校正。
    /// 通常要求夹具处于短路状态，由仪表采集短路补偿数据。
    /// </summary>
    ValueTask ExecuteShortCorrectionAsync(InstrumentCorrectionConditionRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取短路校正后的仪表回传值。
    /// </summary>
    ValueTask<InstrumentCorrectionData> ReadShortCorrectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行负载校正。
    /// 负载校正需要标准件参考值，例如 Ls/Rs，并可覆盖当前频率、电压、量程等测试条件。
    /// </summary>
    ValueTask ExecuteLoadCorrectionAsync(InstrumentLoadCorrectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用已经采集完成的负载校正数据。
    /// 某些仪表需要先执行负载校正，再显式开启校正功能。
    /// </summary>
    ValueTask EnableLoadCorrectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取负载校正后的仪表回传值。
    /// </summary>
    ValueTask<InstrumentCorrectionData> ReadLoadCorrectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 负载校正请求参数。
/// Primary/Secondary 分别对应当前负载类型的第一个、第二个测量项，例如 Ls-Rs 中的 Ls 与 Rs。
/// </summary>
public sealed record InstrumentCorrectionConditionRequest(
    double? Frequency = null,
    string? FrequencyUnit = null,
    double? Voltage = null,
    string? VoltageUnit = null,
    string? Range = null,
    int Spot = 1);
public sealed record InstrumentLoadCorrectionRequest(
    double PrimaryReferenceValue,
    double SecondaryReferenceValue,
    string? LoadType = null,
    double? Frequency = null,
    string? FrequencyUnit = null,
    double? Voltage = null,
    string? VoltageUnit = null,
    string? Range = null,
    int Spot = 1);

/// <summary>
/// 仪表校正回传数据。
/// Primary/Secondary 分别对应当前校正动作返回的两个主要结果值；RawText 保留仪表原始响应，便于排查现场问题。
/// </summary>
public sealed record InstrumentCorrectionData(
    double PrimaryValue,
    double SecondaryValue,
    string RawText,
    string? PrimaryName = null,
    string? SecondaryName = null);