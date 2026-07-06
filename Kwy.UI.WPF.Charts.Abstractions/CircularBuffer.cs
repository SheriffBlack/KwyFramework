using System.Runtime.InteropServices;

namespace Kwy.UI.WPF.Charts.Abstractions;

public sealed class CircularBuffer<T>
{
    private readonly T[] buffer;
    private int head;
    private int tail;
    private int count;

    public CircularBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        }

        buffer = new T[capacity];
    }

    public int Count => count;

    public void Enqueue(T item)
    {
        if (count == buffer.Length)
        {
            buffer[tail] = item;
            tail = (tail + 1) % buffer.Length;
            head = tail;
            return;
        }

        buffer[tail] = item;
        tail = (tail + 1) % buffer.Length;
        count++;
    }

    public T? GetLast()
    {
        if (count == 0)
        {
            return default;
        }

        int lastIndex = (tail - 1 + buffer.Length) % buffer.Length;
        return buffer[lastIndex];
    }

    public void DequeueWhile(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        while (count > 0 && predicate(buffer[head]))
        {
            head = (head + 1) % buffer.Length;
            count--;
        }
    }

    public void Clear()
    {
        head = 0;
        tail = 0;
        count = 0;
    }

    public void CopyToList(List<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        int targetCount = count;
        int diff = list.Count - targetCount;

        if (diff > 0)
        {
            list.RemoveRange(targetCount, diff);
        }
        else if (diff < 0)
        {
            for (int i = 0; i < -diff; i++)
            {
                list.Add(default!);
            }
        }

        if (targetCount == 0)
        {
            return;
        }

        var span = CollectionsMarshal.AsSpan(list);
        if (head < tail)
        {
            buffer.AsSpan(head, targetCount).CopyTo(span);
            return;
        }

        int firstPart = buffer.Length - head;
        buffer.AsSpan(head, firstPart).CopyTo(span[..firstPart]);
        buffer.AsSpan(0, tail).CopyTo(span[firstPart..]);
    }
}
