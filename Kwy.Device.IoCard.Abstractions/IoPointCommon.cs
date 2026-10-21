namespace Kwy.Device.IoCard.Abstractions;

/// <summary>IO 点位对工艺运行的重要程度；不能替代硬件功能安全回路。</summary>
public enum IoPointCriticality
{
    Standard,
    ProcessCritical,
    DiagnosticOnly
}

/// <summary>数字量 IO 通道约定。</summary>
public static class DigitalIoChannelLimits
{
    /// <summary>默认通道数，也是当前 64 位快照可表达的最大通道数。</summary>
    public const int DefaultChannelCount = 64;
}
