namespace Kwy.Communicate.Gem;

/// <summary>适合学习、测试和短暂断线恢复的内存 Spool Store。</summary>
public sealed class InMemoryGemSpoolStore : IGemSpoolStore
{
    private readonly Queue<GemSpooledMessage> messages = new();
    private bool disposed;

    public int Count => messages.Count;
    public long LastSequence { get; private set; }
    public IReadOnlyCollection<GemSpooledMessage> Snapshot => messages.ToArray();

    public void Enqueue(GemSpooledMessage message)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        if (message.Sequence <= LastSequence)
            throw new InvalidOperationException("GEM spool sequence must be strictly increasing.");
        messages.Enqueue(message);
        LastSequence = message.Sequence;
    }

    public GemSpooledMessage? PeekHead()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return messages.Count == 0 ? null : messages.Peek();
    }

    public GemSpooledMessage RemoveHead()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (messages.Count == 0)
            throw new InvalidOperationException("The GEM spool is empty.");
        return messages.Dequeue();
    }

    public void Purge()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PurgeCore();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        PurgeCore();
        disposed = true;
    }

    private void PurgeCore()
    {
        while (messages.Count > 0)
            messages.Dequeue().Message.Dispose();
    }
}
