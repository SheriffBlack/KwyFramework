namespace Kwy.Communicate.OpcUa;

/// <summary>OPC UA 监控项的数据变化消息。</summary>
public sealed record OpcUaMonitoredItemMessage(string NodeId, object? Value, DateTime SourceTimestamp = default);
