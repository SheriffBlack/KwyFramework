using Kwy.UI.WPF.Components.Logging;

namespace KwyTemplate.Vision.ViewModels;

public sealed class FlowRunLogEntry : IKwyLogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;

    public string Level { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;

    public string TimeText => Time.ToString("HH:mm:ss.fff");
}
