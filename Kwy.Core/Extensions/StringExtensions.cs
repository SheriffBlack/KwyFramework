using System.Text;

namespace Kwy.Core.Extensions;

public static class StringExtensions
{
    // 十六进制字符串 转 byte[]
    public static byte[] HexToBytes(this string hexString)
    {
        if (string.IsNullOrWhiteSpace(hexString)) return Array.Empty<byte>();

        // 移除空格并验证格式
        hexString = hexString.Replace(" ", "");
        if (hexString.Length % 2 != 0)
        {
            throw new ArgumentException("十六进制字符串长度必须为偶数");
        }

        // 转换为字节数组
        return Enumerable.Range(0, hexString.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hexString.Substring(x, 2), 16))
                         .ToArray();
    }

    // 普通字符串 转 byte[]
    public static byte[] ToUtf8Bytes(this string command, Encoding? encoding = null)
    {
        if (string.IsNullOrEmpty(command)) return Array.Empty<byte>();
        return (encoding ?? Encoding.UTF8).GetBytes(command);
    }
}
