namespace Kwy.Communicate.Gem;

/// <summary>定义 GEM Spool 的有序存储边界。</summary>
/// <remarks>
/// Enqueue 后消息所有权转移给 Store；RemoveHead 返回后所有权转移给调用方；
/// Purge 和 Dispose 必须释放仍由 Store 拥有的消息。
/// 持久化实现应在 Enqueue 返回前完成可靠写入，并在 PeekHead 时重建独立消息。
/// </remarks>
public interface IGemSpoolStore : IDisposable
{
    int Count { get; }

    long LastSequence { get; }

    IReadOnlyCollection<GemSpooledMessage> Snapshot { get; }

    void Enqueue(GemSpooledMessage message);

    GemSpooledMessage? PeekHead();

    GemSpooledMessage RemoveHead();

    void Purge();
}

