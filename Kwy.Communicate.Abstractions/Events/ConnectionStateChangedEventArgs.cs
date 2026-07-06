using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Communicate.Abstractions.Events;

/// <summary>
/// 连接状态改变事件参数
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 之前的连接状态
    /// </summary>
    public ConnectionState PreviousState { get; }

    /// <summary>
    /// 当前的连接状态
    /// </summary>
    public ConnectionState CurrentState { get; }

    public ConnectionStateChangedEventArgs(ConnectionState previousState, ConnectionState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }
}
