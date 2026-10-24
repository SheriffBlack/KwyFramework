using System.Text;

namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 为 <see cref="IByteStreamClient"/> 提供不包含具体协议规则的通用辅助方法。
/// </summary>
public static class ByteStreamClientExtensions
{
    /// <summary>同步写入字节。仅建议用于调试工具或必须同步调用的兼容场景。</summary>
    public static void Write(this IByteStreamClient client, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.WriteAsync(data.ToArray()).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>同步读取字节，返回值可小于缓冲区长度。仅建议用于同步兼容场景。</summary>
    public static int Read(this IByteStreamClient client, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (buffer.IsEmpty)
            return 0;

        var data = new byte[buffer.Length];
        int length = client.ReadAsync(data).AsTask().GetAwaiter().GetResult();
        ValidateReadLength(length, data.Length);
        data.AsSpan(0, length).CopyTo(buffer);
        return length;
    }

    /// <summary>按指定编码写入文本。默认使用 UTF-8；工业协议建议显式指定编码。</summary>
    public static ValueTask WriteTextAsync(
        this IByteStreamClient client,
        string text,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(text);
        return client.WriteAsync((encoding ?? Encoding.UTF8).GetBytes(text), cancellationToken);
    }

    /// <summary>同步写入文本。仅建议用于同步兼容场景。</summary>
    public static void WriteText(this IByteStreamClient client, string text, Encoding? encoding = null)
        => client.WriteTextAsync(text, encoding).AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// 持续读取直到填满 <paramref name="buffer"/>。用于固定长度帧头或已知长度的数据体，
    /// 不定义文本结束符等协议边界。
    /// </summary>
    /// <exception cref="EndOfStreamException">字节流客户端在填满缓冲区前返回零字节。</exception>
    public static async ValueTask ReadExactlyAsync(
        this IByteStreamClient client,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await client.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            ValidateReadLength(bytesRead, buffer.Length - totalRead);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    $"The byte stream ended after {totalRead} of {buffer.Length} bytes were read.");
            }

            totalRead += bytesRead;
        }
    }

    private static void ValidateReadLength(int length, int bufferLength)
    {
        if ((uint)length > (uint)bufferLength)
        {
            throw new InvalidDataException(
                $"The byte stream client returned an invalid byte count ({length}) for a {bufferLength}-byte buffer.");
        }
    }
}
