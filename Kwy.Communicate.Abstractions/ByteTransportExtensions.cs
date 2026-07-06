using System.Text;

namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Optional synchronous and text helpers for byte transports.
/// </summary>
public static class ByteTransportExtensions
{
    public static void Write(this IByteTransport transport, ReadOnlySpan<byte> data)
        => transport.WriteAsync(data.ToArray()).AsTask().GetAwaiter().GetResult();

    public static int Read(this IByteTransport transport, Span<byte> buffer)
    {
        var data = new byte[buffer.Length];
        var length = transport.ReadAsync(data).AsTask().GetAwaiter().GetResult();
        data.AsSpan(0, length).CopyTo(buffer);
        return length;
    }

    public static ValueTask WriteTextAsync(
        this IByteTransport transport,
        string text,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return transport.WriteAsync((encoding ?? Encoding.UTF8).GetBytes(text), cancellationToken);
    }

    public static void WriteText(this IByteTransport transport, string text, Encoding? encoding = null)
        => transport.WriteTextAsync(text, encoding).AsTask().GetAwaiter().GetResult();
}
