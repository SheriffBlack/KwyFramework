using Kwy.Device.Abstractions.Instrument;

namespace KwyTemplate.Flow.Models;

/// <summary>
/// 机台点检流程返回值：既表达本次点检是否完成，也带回每个仪表的实际测量结果。
/// </summary>
public sealed record MachineExamineResult(
    bool IsCompleted,
    IReadOnlyList<MachineExamineMeasurement> Measurements,
    string? Message = null)
{
    public static MachineExamineResult Failed(string? message = null, IReadOnlyList<MachineExamineMeasurement>? measurements = null)
        => new(false, measurements ?? [], message);

    public static MachineExamineResult Completed(IReadOnlyList<MachineExamineMeasurement> measurements, string? message = null)
        => new(true, measurements, message);
}

public sealed record MachineExamineMeasurement(
    int StationId,
    string StationName,
    string InstrumentCode,
    InstrumentMeasurementResult Measurement);

/// <summary>
/// 机台点检流程描述。具体机型只声明点位和工站步骤，执行细节由 MachineBase 统一处理。
/// </summary>
public sealed record MachineExamineFlowDescriptor(
    string Code,
    int SamplePointKey,
    int StartPointKey,
    int CompletedPointKey,
    IReadOnlyList<MachineExamineStepDescriptor> Steps,
    int RepeatCount = 1);

/// <summary>
/// 点检流程中的单个仪表步骤。
/// </summary>
public sealed record MachineExamineStepDescriptor(
    int TriggerPointKey,
    int ReadCompletedPointKey,
    int StationId,
    string TestName,
    int TimeoutMs = 10_000);

