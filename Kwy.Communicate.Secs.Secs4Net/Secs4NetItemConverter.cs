using KwyItem = Kwy.Communicate.Secs.SecsItem;
using KwyItemFormat = Kwy.Communicate.Secs.SecsItemFormat;
using Secs4NetItem = global::Secs4Net.Item;

namespace Kwy.Communicate.Secs.Secs4Net;

internal static class Secs4NetItemConverter
{
    public static Secs4NetItem? ToSecs4Net(KwyItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return item.Format switch
        {
            KwyItemFormat.List => Secs4NetItem.L(item.Children.Select(ToSecs4Net).Where(static child => child is not null).Cast<Secs4NetItem>().ToArray()),
            KwyItemFormat.Binary => Secs4NetItem.B(GetArray<byte>(item.Value)),
            KwyItemFormat.Boolean => Secs4NetItem.Boolean(GetArray<bool>(item.Value)),
            KwyItemFormat.Ascii => Secs4NetItem.A(item.Value as string ?? string.Empty),
            KwyItemFormat.Jis8 => Secs4NetItem.J(item.Value as string ?? string.Empty),
            KwyItemFormat.Int1 => Secs4NetItem.I1(GetArray<sbyte>(item.Value)),
            KwyItemFormat.Int2 => Secs4NetItem.I2(GetArray<short>(item.Value)),
            KwyItemFormat.Int4 => Secs4NetItem.I4(GetArray<int>(item.Value)),
            KwyItemFormat.Int8 => Secs4NetItem.I8(GetArray<long>(item.Value)),
            KwyItemFormat.UInt1 => Secs4NetItem.U1(GetArray<byte>(item.Value)),
            KwyItemFormat.UInt2 => Secs4NetItem.U2(GetArray<ushort>(item.Value)),
            KwyItemFormat.UInt4 => Secs4NetItem.U4(GetArray<uint>(item.Value)),
            KwyItemFormat.UInt8 => Secs4NetItem.U8(GetArray<ulong>(item.Value)),
            KwyItemFormat.Float4 => Secs4NetItem.F4(GetArray<float>(item.Value)),
            KwyItemFormat.Float8 => Secs4NetItem.F8(GetArray<double>(item.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.Format, "Unsupported Kwy SECS item format.")
        };
    }

    public static KwyItem? FromSecs4Net(Secs4NetItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return item.Format switch
        {
            global::Secs4Net.SecsFormat.List => KwyItem.L(item.Items.Select(FromSecs4Net).Where(static child => child is not null).Cast<KwyItem>().ToArray()),
            global::Secs4Net.SecsFormat.Binary => KwyItem.B(item.GetMemory<byte>().ToArray()),
            global::Secs4Net.SecsFormat.Boolean => KwyItem.Bool(item.GetMemory<bool>().ToArray()),
            global::Secs4Net.SecsFormat.ASCII => KwyItem.A(item.GetString()),
            global::Secs4Net.SecsFormat.JIS8 => new KwyItem(KwyItemFormat.Jis8, item.GetString()),
            global::Secs4Net.SecsFormat.I1 => KwyItem.I1(item.GetMemory<sbyte>().ToArray()),
            global::Secs4Net.SecsFormat.I2 => KwyItem.I2(item.GetMemory<short>().ToArray()),
            global::Secs4Net.SecsFormat.I4 => KwyItem.I4(item.GetMemory<int>().ToArray()),
            global::Secs4Net.SecsFormat.I8 => new KwyItem(KwyItemFormat.Int8, item.GetMemory<long>().ToArray()),
            global::Secs4Net.SecsFormat.U1 => KwyItem.U1(item.GetMemory<byte>().ToArray()),
            global::Secs4Net.SecsFormat.U2 => KwyItem.U2(item.GetMemory<ushort>().ToArray()),
            global::Secs4Net.SecsFormat.U4 => KwyItem.U4(item.GetMemory<uint>().ToArray()),
            global::Secs4Net.SecsFormat.U8 => new KwyItem(KwyItemFormat.UInt8, item.GetMemory<ulong>().ToArray()),
            global::Secs4Net.SecsFormat.F4 => KwyItem.F4(item.GetMemory<float>().ToArray()),
            global::Secs4Net.SecsFormat.F8 => KwyItem.F8(item.GetMemory<double>().ToArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.Format, "Unsupported Secs4Net item format.")
        };
    }

    private static T[] GetArray<T>(object? value)
    {
        return value switch
        {
            null => Array.Empty<T>(),
            T[] array => array,
            IEnumerable<T> enumerable => enumerable.ToArray(),
            T single => new[] { single },
            _ => throw new ArgumentException($"Value cannot be converted to {typeof(T).Name} array.", nameof(value))
        };
    }
}
