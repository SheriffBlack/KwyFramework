namespace Kwy.MVVM.Messaging;

/// <summary>
/// 指定消息处理程序的执行线程。
/// </summary>
public enum MessageThread
{
    /// <summary>
    /// 在消息发布线程上执行。
    /// </summary>
    Publisher,

    /// <summary>
    /// 在已注册的 UI 线程调度器上执行。
    /// </summary>
    UI,

    /// <summary>
    /// 在线程池上执行。
    /// </summary>
    Background
}
