using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>提供有界、线程安全且所有权明确的内存 GEM Spool。</summary>
/// <remarks>
/// 该实现不提供断电恢复。生产项目需要持久化时，应在应用层保存等价的业务事件，
/// 或实现经过客户规范确认的持久化 Spool。
/// </remarks>
public sealed class GemSpoolingService : IDisposable
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim replayGate = new(1, 1);
    private readonly IGemSpoolStore store;
    private long sequence;
    private bool disposed;

    public GemSpoolingService(
        GemSpoolingOptions? options = null,
        IGemSpoolStore? store = null)
    {
        this.store = store ?? new InMemoryGemSpoolStore();
        Options = Validate(options ?? new GemSpoolingOptions());
        State = Options.Enabled ? GemSpoolingState.Enabled : GemSpoolingState.Disabled;
        sequence = this.store.LastSequence;
        TrimUnsafe();
    }

    public GemSpoolingOptions Options { get; private set; }

    public GemSpoolingState State { get; private set; }

    public int Count
    {
        get { lock (syncRoot) return store.Count; }
    }

    /// <summary>诊断快照中的消息仍由 Spool 拥有，调用方不得释放或修改。</summary>
    public IReadOnlyCollection<GemSpooledMessage> Messages
    {
        get { lock (syncRoot) return store.Snapshot; }
    }

    public void Configure(GemSpoolingOptions options)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        options = Validate(options);
        replayGate.Wait();
        try
        {
            lock (syncRoot)
            {
                Options = options;
                State = options.Enabled ? GemSpoolingState.Enabled : GemSpoolingState.Disabled;
                TrimUnsafe();
                if (!options.Enabled)
                    PurgeUnsafe();
            }
        }
        finally
        {
            replayGate.Release();
        }
    }

    /// <summary>将独立副本入队；false 表示该消息或失败类型不允许进入 Spool。</summary>
    public bool TryEnqueue(SecsMessage message, bool ambiguousSendFailure = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        lock (syncRoot)
        {
            if (!Options.Enabled ||
                ambiguousSendFailure && !Options.SpoolOnAmbiguousSendFailure ||
                !IsEligibleUnsafe(message))
                return false;

            if (store.Count >= Options.MaximumMessages)
            {
                if (Options.OverflowPolicy == GemSpoolOverflowPolicy.RejectNewest)
                    return false;
                store.RemoveHead().Message.Dispose();
            }

            State = GemSpoolingState.Active;
            store.Enqueue(new GemSpooledMessage(
                ++sequence,
                CloneMessage(message),
                DateTimeOffset.Now));
            TrimUnsafe();
            return true;
        }
    }

    public void Enqueue(SecsMessage message) => TryEnqueue(message);

    /// <summary>按序补传，只在发送成功后移除；失败消息及其后续消息保留。</summary>
    public async Task<GemSpoolReplayResult> ReplayAsync(
        Func<SecsMessage, CancellationToken, Task> sender,
        int maximumMessages = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(sender);
        if (maximumMessages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMessages));

        await replayGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int attempted = 0;
            int sent = 0;
            while (attempted < maximumMessages)
            {
                GemSpooledMessage? current;
                lock (syncRoot)
                {
                    current = store.PeekHead();
                    if (current is null)
                    {
                        UpdateIdleStateUnsafe();
                        return new(attempted, sent, 0);
                    }
                    State = GemSpoolingState.Transmitting;
                }

                cancellationToken.ThrowIfCancellationRequested();
                attempted++;
                try
                {
                    await sender(current.Message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lock (syncRoot)
                    {
                        State = GemSpoolingState.Active;
                        return new(attempted, sent, store.Count, ex);
                    }
                }

                lock (syncRoot)
                {
                    if (store.PeekHead()?.Sequence != current.Sequence)
                        throw new InvalidOperationException("The GEM spool head changed while replaying.");
                    store.RemoveHead().Message.Dispose();
                    sent++;
                    UpdateIdleStateUnsafe();
                }
            }

            return new(attempted, sent, Count);
        }
        finally
        {
            replayGate.Release();
        }
    }

    public void Purge()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        replayGate.Wait();
        try
        {
            lock (syncRoot)
            {
                State = GemSpoolingState.Purging;
                PurgeUnsafe();
                UpdateIdleStateUnsafe();
            }
        }
        finally
        {
            replayGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        replayGate.Wait();
        try
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;
                disposed = true;
                PurgeUnsafe();
                store.Dispose();
                State = GemSpoolingState.Disabled;
            }
        }
        finally
        {
            replayGate.Release();
        }
    }

    private bool IsEligibleUnsafe(SecsMessage message)
        => (message.S, message.F) switch
        {
            (6, 11) => true,
            (5, 1) => Options.IncludeAlarms,
            (6, 1) => Options.IncludeTraceData,
            (10, 1) => Options.IncludeTerminalMessages,
            _ => false
        };

    private static SecsMessage CloneMessage(SecsMessage message)
        => new(message.S, message.F, message.ReplyExpected)
        {
            Name = message.Name,
            SecsItem = message.SecsItem is null ? null : GemItemCloner.Clone(message.SecsItem)
        };

    private void TrimUnsafe()
    {
        while (store.Count > Options.MaximumMessages)
            store.RemoveHead().Message.Dispose();
        UpdateIdleStateUnsafe();
    }

    private void PurgeUnsafe()
    {
        store.Purge();
    }

    private void UpdateIdleStateUnsafe()
        => State = !Options.Enabled
            ? GemSpoolingState.Disabled
            : store.Count == 0
                ? GemSpoolingState.Enabled
                : GemSpoolingState.Active;

    private static GemSpoolingOptions Validate(GemSpoolingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumMessages <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumMessages must be greater than zero.");
        return options;
    }
}
