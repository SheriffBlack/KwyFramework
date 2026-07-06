using System.Text;

namespace Kwy.Core.Extensions;

public static class ByteExtensions
{
    // byte[] 转 十六进制字符串
    public static string ToHexString(this byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;

        StringBuilder sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.AppendFormat("0x{0:X2} ", b);
        }
        return sb.ToString().Trim();
    }
    // byte[] 转 普通字符串
    public static string ToUtf8String(this byte[] data, Encoding? encoding = null)
    {
        if (data == null || data.Length == 0) return string.Empty;
        return (encoding ?? Encoding.UTF8).GetString(data);
    }
}
