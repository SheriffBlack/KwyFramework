namespace Kwy.UI.WPF.Components.ActivityLog;

/// <summary>
/// UI 活动日志项的最小显示契约。
/// </summary>
public interface IKwyActivityLogEntry
{
    string TimeText { get; }

    string Level { get; }

    string Message { get; }
}

