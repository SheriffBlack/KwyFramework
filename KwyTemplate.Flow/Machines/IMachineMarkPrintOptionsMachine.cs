namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 支持将标记打印字符串写入外部打印机的机器功能
/// </summary>
public interface IMachineMarkPrintOptionsMachine
{
    Task ApplyMarkPrintStringAsync(string? printString, CancellationToken cancellationToken = default);
}