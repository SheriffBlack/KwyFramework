using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Kwy.UI.WPF.Components.Logging;

/// <summary>
/// 面向界面的轻量日志缓冲区，用于把运行期消息推送到 KwyLogListView。
/// </summary>
public sealed class KwyLogService
{
    private const int DefaultMaxCount = 2000;
    
    private readonly Dispatcher dispatcher;
private long nextSequence;
    private int maxCount = DefaultMaxCount;

    public ObservableCollection<KwyLogEntry> Entries { get; } = [];

    
    public KwyLogService()
    {
        dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }
/// <summary>
    /// LogView 只保留最近日志，完整历史由文件日志负责持久化。
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

    public void AddStartupProgress(string level, string message, double progressValue)
        => Add(level, message, progressValue);

    public void Add(string level, string message, double? sortOrder = null)
        => RunOnUi(() => AddCore(level, message, sortOrder));

    private void RunOnUi(Action action)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
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
            _ = dispatcher.BeginInvoke(() => SafeInvoke(action), DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            // 应用正在关闭时，Dispatcher 可能已经拒绝新的 UI 任务；界面日志直接丢弃。
        }
        catch (TaskCanceledException)
        {
            // Dispatcher 关闭过程中可能取消排队任务；不要让日志影响程序退出。
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
            // 关闭阶段 UI 集合可能已经不可访问；日志服务不向外传播该异常。
        }
        catch (TaskCanceledException)
        {
            // 同上，关闭阶段取消属于正常释放路径。
        }
        catch (NotSupportedException)
        {
            // CollectionView 关闭或跨线程保护触发时，不让界面日志影响业务流程。
        }
    }

    private void AddCore(string level, string message, double? sortOrder)
    {
        var entry = new KwyLogEntry
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

        for (int i = 1; i < Entries.Count; i++)
        {
            if (Entries[i].Sequence < oldestSequence)
            {
                oldestSequence = Entries[i].Sequence;
                oldestIndex = i;
            }
        }

        Entries.RemoveAt(oldestIndex);
    }

    private int GetSortedInsertIndex(KwyLogEntry entry)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            KwyLogEntry current = Entries[i];
            if (!current.SortOrder.HasValue)
            {
                return i;
            }

            int orderComparison = current.SortOrder.Value.CompareTo(entry.SortOrder!.Value);
            if (orderComparison > 0 || (orderComparison == 0 && current.Sequence > entry.Sequence))
            {
                return i;
            }
        }

        return Entries.Count;
    }
}
