using System;

namespace Kwy.Communicate.OpcUa;

/// <summary>
/// OPC UA Monitored Item Data Change Message.
/// </summary>
public sealed record OpcUaMonitoredItemMessage(string NodeId, object? Value, DateTime SourceTimestamp = default);
