namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 工单参数写入到仪表的结果
/// </summary>
public sealed class WorkOrderHardwareWriteResult
{
    private readonly List<WorkOrderHardwareWriteFailure> failures = [];

    public IReadOnlyList<WorkOrderHardwareWriteFailure> Failures => failures;

    public bool IsSuccess => failures.Count == 0;

    public void AddFailure(string target, Exception exception)
        => failures.Add(new WorkOrderHardwareWriteFailure(target, exception.Message));
}

public sealed record WorkOrderHardwareWriteFailure(string Target, string Message);
