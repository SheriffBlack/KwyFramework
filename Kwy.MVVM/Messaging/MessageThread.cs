namespace Kwy.MVVM.Messaging;

/// <summary>
/// Specifies where a message handler should run.
/// </summary>
public enum MessageThread
{
    /// <summary>
    /// Runs on the publishing thread.
    /// </summary>
    Publisher,

    /// <summary>
    /// Runs on the UI thread dispatcher registered by the platform layer.
    /// </summary>
    UI,

    /// <summary>
    /// Runs on the thread pool.
    /// </summary>
    Background
}
