using KwyTemplate.Vision.ViewModels.Items;

namespace KwyTemplate.Vision.Cache;

public sealed class VisionImageCache
{
    private readonly int maxCount;
    private readonly long maxMemoryBytes;
    private readonly LinkedList<CacheNode> lruList = new();
    private readonly Dictionary<string, LinkedListNode<CacheNode>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();
    private long currentMemoryBytes;

    public VisionImageCache(int maxCount = 50, long maxMemoryMb = 500)
    {
        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        if (maxMemoryMb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemoryMb));
        }

        this.maxCount = maxCount;
        maxMemoryBytes = checked(maxMemoryMb * 1024 * 1024);
    }

    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return cache.Count;
            }
        }
    }

    public long CurrentMemoryBytes
    {
        get
        {
            lock (syncRoot)
            {
                return currentMemoryBytes;
            }
        }
    }

    public VisionImagePanelItemViewModel? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (syncRoot)
        {
            if (!cache.TryGetValue(key, out LinkedListNode<CacheNode>? node))
            {
                return null;
            }

            lruList.Remove(node);
            lruList.AddFirst(node);
            return node.Value.Item;
        }
    }

    public void Add(string key, VisionImagePanelItemViewModel item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(item);

        long itemSize = EstimateSize(item);
        lock (syncRoot)
        {
            if (cache.TryGetValue(key, out LinkedListNode<CacheNode>? existing))
            {
                RemoveNode(existing);
            }

            while (cache.Count >= maxCount ||
                   currentMemoryBytes + itemSize > maxMemoryBytes && cache.Count > 0)
            {
                LinkedListNode<CacheNode>? last = lruList.Last;
                if (last == null)
                {
                    break;
                }

                RemoveNode(last);
            }

            var node = lruList.AddFirst(new CacheNode(key, item, itemSize));
            cache[key] = node;
            currentMemoryBytes += itemSize;
        }
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (syncRoot)
        {
            if (!cache.TryGetValue(key, out LinkedListNode<CacheNode>? node))
            {
                return false;
            }

            RemoveNode(node);
            return true;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            cache.Clear();
            lruList.Clear();
            currentMemoryBytes = 0;
        }
    }

    private void RemoveNode(LinkedListNode<CacheNode> node)
    {
        lruList.Remove(node);
        cache.Remove(node.Value.Key);
        currentMemoryBytes -= node.Value.SizeBytes;
    }

    private static long EstimateSize(VisionImagePanelItemViewModel item)
        => item.Pixels?.LongLength ?? 0;

    private sealed record CacheNode(string Key, VisionImagePanelItemViewModel Item, long SizeBytes);
}

