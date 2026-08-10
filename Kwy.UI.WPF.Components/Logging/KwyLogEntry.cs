namespace Kwy.UI.WPF.Components.Logging;

/// <summary>
/// Kwy UI 通用日志项。
/// </summary>
public sealed class KwyLogEntry : IKwyLogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;

    public string Level { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;

    public double? SortOrder { get; init; }

    public long Sequence { get; init; }

    public string TimeText => Time.ToString("HH:mm:ss.fff");
}