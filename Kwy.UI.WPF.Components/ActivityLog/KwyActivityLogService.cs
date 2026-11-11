using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Kwy.UI.WPF.Components.ActivityLog;

/// <summary>
/// 面向操作员界面的轻量活动日志缓冲区。
/// 只保存用户可见的运行摘要，不代替 ILogger 或持久化日志。
/// </summary>
public sealed class KwyActivityLogService
{
    private const int DefaultMaxCount = 2000;

    private readonly Dispatcher dispatcher;
    private long nextSequence;
    private int maxCount = DefaultMaxCount;

    public KwyActivityLogService()
    {
        dispatcher = Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;
    }

    public ObservableCollection<KwyActivityLogEntry> Entries { get; } = [];

    /// <summary>
    /// 获取或设置界面最多保留的活动日志数量。
    /// 完整历史应由 ILogger 对应的持久化日志提供程序保存。
    /// </summary>
    public int MaxCount
    {
        get => maxCount;
        set
        {
            maxCount = Math.Max(1, value);
            RunOnUi(TrimToMaxCount);
        }
    }

    public void Info(string message) => Add("Info", message);

    public void Success(string message) => Add("Success", message);

    public void Warn(string message) => Add("Warn", message);

    public void Error(string message) => Add("Error", message);

    public void AddStartupProgress(
        string level,
        string message,
        double progressValue)
        => Add(level, message, progressValue);

    public void Add(
        string level,
        string message,
        double? sortOrder = null)
        => RunOnUi(() => AddCore(level, message, sortOrder));

    private void RunOnUi(Action action)
    {
        if (dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            SafeInvoke(action);
            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(
                () => SafeInvoke(action),
                DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            // 应用正在关闭时，Dispatcher 可能已经拒绝新的 UI 任务。
        }
        catch (TaskCanceledException)
        {
            // Dispatcher 关闭阶段的排队任务取消属于正常释放路径。
        }
    }

    private static void SafeInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            // 关闭阶段 UI 集合可能已经不可访问，不影响业务退出。
        }
        catch (TaskCanceledException)
        {
            // 关闭阶段取消属于正常释放路径。
        }
        catch (NotSupportedException)
        {
            // CollectionView 关闭或跨线程保护触发时，不影响业务流程。
        }
    }

    private void AddCore(
        string level,
        string message,
        double? sortOrder)
    {
        var entry = new KwyActivityLogEntry
        {
            Time = DateTime.Now,
            Level = string.IsNullOrWhiteSpace(level) ? "Info" : level,
            Message = message ?? string.Empty,
            SortOrder = sortOrder,
            Sequence = ++nextSequence
        };

        if (sortOrder.HasValue)
        {
            Entries.Insert(GetSortedInsertIndex(entry), entry);
        }
        else
        {
            Entries.Add(entry);
        }

        TrimToMaxCount();
    }

    private void TrimToMaxCount()
    {
        while (Entries.Count > maxCount)
        {
            RemoveOldestEntry();
        }
    }

    private void RemoveOldestEntry()
    {
        int oldestIndex = 0;
        long oldestSequence = Entries[0].Sequence;

        for (int index = 1; index < Entries.Count; index++)
        {
            if (Entries[index].Sequence >= oldestSequence)
            {
                continue;
            }

            oldestSequence = Entries[index].Sequence;
            oldestIndex = index;
        }

        Entries.RemoveAt(oldestIndex);
    }

    private int GetSortedInsertIndex(KwyActivityLogEntry entry)
    {
        for (int index = 0; index < Entries.Count; index++)
        {
            KwyActivityLogEntry current = Entries[index];
            if (!current.SortOrder.HasValue)
            {
                return index;
            }

            int orderComparison = current.SortOrder.Value
                .CompareTo(entry.SortOrder!.Value);

            if (orderComparison > 0
                || orderComparison == 0
                && current.Sequence > entry.Sequence)
            {
                return index;
            }
        }

        return Entries.Count;
    }
}

