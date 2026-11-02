using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed record GemParseResult<T>(T? Value, string? Error = null)
{
    public bool IsSuccess => Error is null;

    public static GemParseResult<T> Success(T value) => new(value);

    public static GemParseResult<T> Failure(string error) => new(default, error);
}

/// <summary>将一条 SECS Primary Message 解析为强类型请求。</summary>
public interface IGemMessageParser<T>
{
    GemParseResult<T> Parse(SecsMessage message);
}

internal static class GemItemReader
{
    public static bool TryList(Item? item, int? count, out IReadOnlyList<Item> items, out string? error)
    {
        if (item is null || item.Format != SecsFormat.List)
        {
            items = [];
            error = "Expected a SECS-II List item.";
            return false;
        }

        items = item.Items;
        if (count is int expected && items.Count != expected)
        {
            error = $"Expected {expected} list items, but received {items.Count}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryUInt32(Item? item, out uint value)
    {
        value = 0;
        if (item is null || item.Format is not (
                SecsFormat.U1 or SecsFormat.U2 or SecsFormat.U4 or SecsFormat.U8 or
                SecsFormat.I1 or SecsFormat.I2 or SecsFormat.I4 or SecsFormat.I8))
        {
            return false;
        }

        if (item.Count != 1)
            return false;

        try
        {
            value = item.Format switch
            {
                SecsFormat.U1 => item.FirstValue<byte>(),
                SecsFormat.U2 => item.FirstValue<ushort>(),
                SecsFormat.U4 => item.FirstValue<uint>(),
                SecsFormat.U8 => checked((uint)item.FirstValue<ulong>()),
                SecsFormat.I1 => checked((uint)item.FirstValue<sbyte>()),
                SecsFormat.I2 => checked((uint)item.FirstValue<short>()),
                SecsFormat.I4 => checked((uint)item.FirstValue<int>()),
                SecsFormat.I8 => checked((uint)item.FirstValue<long>()),
                _ => 0
            };
            return true;
        }
        catch (Exception exception) when (exception is
            IndexOutOfRangeException or
            OverflowException or
            NotSupportedException)
        {
            return false;
        }
    }
}
