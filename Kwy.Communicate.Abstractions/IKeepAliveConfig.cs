namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Optional active health-check configuration for protocols that need it.
/// </summary>
public interface IKeepAliveConfig
{
    /// <summary>
    /// Whether to enable active connection health checks.
    /// </summary>
    bool KeepAlive { get; set; }

    /// <summary>
    /// Health-check interval in milliseconds.
    /// </summary>
    int KeepAliveInterval { get; set; }
}
