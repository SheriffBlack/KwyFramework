namespace Kwy.Core.Threading;

public sealed class BackgroundTaskErrorReporter : IBackgroundTaskErrorReporter
{
    public event EventHandler<BackgroundTaskError>? ErrorReported;

    public void Report(BackgroundTaskError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ErrorReported?.Invoke(this, error);
    }
}
