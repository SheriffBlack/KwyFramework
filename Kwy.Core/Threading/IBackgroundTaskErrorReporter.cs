namespace Kwy.Core.Threading;

public interface IBackgroundTaskErrorReporter
{
    event EventHandler<BackgroundTaskError>? ErrorReported;

    void Report(BackgroundTaskError error);
}
