using Kwy.UI.WPF.Components.ActivityLog;

namespace KwyTemplate.Vision.ViewModels;

public sealed class FlowRunLogEntry : IKwyActivityLogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;

    public string Level { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;

    public string TimeText => Time.ToString("HH:mm:ss.fff");
}
