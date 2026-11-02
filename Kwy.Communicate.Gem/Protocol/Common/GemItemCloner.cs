using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

/// <summary>为需要跨越报文生命周期保存的数据创建完全独立的 SECS-II Item。</summary>
internal static class GemItemCloner
{
    public static Item Clone(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Format switch
        {
            SecsFormat.List => L(item.Items.Select(Clone).ToArray()),
            SecsFormat.Binary => B(item.GetMemory<byte>().ToArray()),
            SecsFormat.Boolean => Boolean(item.GetMemory<bool>().ToArray()),
            SecsFormat.ASCII => A(item.GetString()),
            SecsFormat.JIS8 => J(item.GetString()),
            SecsFormat.I8 => I8(item.GetMemory<long>().ToArray()),
            SecsFormat.I1 => I1(item.GetMemory<sbyte>().ToArray()),
            SecsFormat.I2 => I2(item.GetMemory<short>().ToArray()),
            SecsFormat.I4 => I4(item.GetMemory<int>().ToArray()),
            SecsFormat.F8 => F8(item.GetMemory<double>().ToArray()),
            SecsFormat.F4 => F4(item.GetMemory<float>().ToArray()),
            SecsFormat.U8 => U8(item.GetMemory<ulong>().ToArray()),
            SecsFormat.U1 => U1(item.GetMemory<byte>().ToArray()),
            SecsFormat.U2 => U2(item.GetMemory<ushort>().ToArray()),
            SecsFormat.U4 => U4(item.GetMemory<uint>().ToArray()),
            _ => throw new NotSupportedException($"SECS format {item.Format} cannot be cloned.")
        };
    }
}
