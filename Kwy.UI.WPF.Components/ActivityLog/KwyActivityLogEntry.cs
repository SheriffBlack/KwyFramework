namespace Kwy.UI.WPF.Components.ActivityLog;

/// <summary>
/// Kwy UI 通用活动日志项。
/// </summary>
public sealed class KwyActivityLogEntry : IKwyActivityLogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;

    public string Level { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;

    public double? SortOrder { get; init; }

    public long Sequence { get; init; }

    public string TimeText => Time.ToString("HH:mm:ss.fff");
}

