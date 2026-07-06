namespace Kwy.Communicate.Abstractions.Events;

/// <summary>
/// 错误发生事件参数
/// </summary>
public class ErrorOccurredEventArgs : EventArgs
{
    /// <summary>
    /// 异常信息
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; }

    public ErrorOccurredEventArgs(Exception exception, string message)
    {
        Exception = exception;
        Message = message;
    }
}
