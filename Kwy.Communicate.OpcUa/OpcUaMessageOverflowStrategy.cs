namespace Kwy.Communicate.OpcUa;

/// <summary>OPC UA 订阅消息缓冲区满时的处理策略。</summary>
public enum OpcUaMessageOverflowStrategy
{
    /// <summary>丢弃缓冲区中最早的消息。</summary>
    DropOldest,
    /// <summary>丢弃缓冲区中最新的消息。</summary>
    DropNewest,
    /// <summary>丢弃当前写入的消息。</summary>
    DropWrite
}
