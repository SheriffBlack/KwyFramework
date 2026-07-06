using System.Globalization;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// 智能十六进制/十进制转换器
/// 支持：
/// - 输入 0x01 显示 0x01，输入 1 显示 1
/// - 自动识别格式并保持用户输入的格式
/// - 初始化时，如果值是常见的十六进制值（如 0x01, 0x00, 0xFF），自动显示为十六进制
/// </summary>
public class HexDecConverter : IValueConverter
{
    // 存储每个值的显示格式（用于保持用户输入的格式）
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> formatCache = new();

    // 常见的十六进制值列表（用于初始化时判断）
    private static readonly HashSet<int> commonHexValues = new()
    {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0,
        0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA,
        0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x000F,
        0x0010, 0x0020, 0x0030, 0x0040, 0x0050, 0x0060, 0x0070, 0x0080, 0x0090, 0x00A0, 0x00B0, 0x00C0, 0x00D0, 0x00E0, 0x00F0,
        0x0100, 0x0200, 0x0300, 0x0400, 0x0500, 0x0600, 0x0700, 0x0800, 0x0900, 0x0A00, 0x0B00, 0x0C00, 0x0D00, 0x0E00, 0x0F00,
        0x1000, 0x2000, 0x3000, 0x4000, 0x5000, 0x6000, 0x7000, 0x8000, 0x9000, 0xA000, 0xB000, 0xC000, 0xD000, 0xE000, 0xF000,
        0xFFFF, 0xFFFE, 0xFFFD, 0xFFFC, 0xFFFB, 0xFFFA
    };

    /// <summary>
    /// 判断值是否应该显示为十六进制（初始化时）
    /// 规则：
    /// 1. 值在常见十六进制值列表中，显示为十六进制
    /// 2. 值小于 16（0x00-0x0F），默认显示为十六进制（因为这是常见的十六进制范围）
    /// 3. 值在 0-255 范围内且是 16 的倍数，可能是十六进制
    /// 4. 值在 256-65535 范围内且是 256 的倍数，可能是十六进制
    /// 5. 其他情况默认显示为十进制
    /// </summary>
    private static bool ShouldDisplayAsHex(int value)
    {
        // 检查是否在常见十六进制值列表中
        if (commonHexValues.Contains(value))
            return true;

        // 小于 16 的值（0x00-0x0F）通常是十六进制
        // 但排除常见的十进制值 1, 2, 3, 10
        if (value < 16)
        {
            // 这些值可能是十进制，也可能是十六进制
            // 为了兼容性，如果值在常见十六进制值列表中，显示为十六进制
            // 否则，对于 0-15 范围，默认显示为十六进制（因为 0x00-0x0F 是常见的）
            return value != 1 && value != 2 && value != 3 && value != 10;
        }

        // 0-255 范围：16 的倍数通常是十六进制
        if (value >= 16 && value <= 255)
        {
            // 16 的倍数通常是十六进制（0x10, 0x20, 0x30 等）
            if (value % 16 == 0)
                return true;
        }

        // 256-65535 范围：256 的倍数通常是十六进制
        if (value >= 256 && value <= 65535)
        {
            // 256 的倍数通常是十六进制（0x0100, 0x0200 等）
            if (value % 256 == 0)
                return true;

            // 小于 4096 (0x1000) 的值，如果是 16 的倍数，可能是十六进制
            if (value < 4096 && value % 16 == 0)
                return true;
        }

        // 其他情况默认显示为十进制
        return false;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        int intValue;
        // 🌟 性能优化：通过模式匹配直接处理常见数值类型，避免 ToString() 和重复解析
        if (value is int i) intValue = i;
        else if (value is byte b) intValue = b;
        else if (value is short s) intValue = s;
        else if (value is uint ui) intValue = (int)ui;
        else if (value is long l) intValue = (int)l;
        else if (int.TryParse(value.ToString(), out int parsed))
        {
            intValue = parsed;
        }
        else
        {
            return value.ToString() ?? string.Empty;
        }

        // 检查缓存中是否有该值的格式记录
        if (formatCache.TryGetValue(intValue, out var cachedFormat))
        {
            return cachedFormat;
        }

        // 如果没有缓存，使用智能判断
        if (ShouldDisplayAsHex(intValue))
        {
            // 根据值的大小决定显示格式
            string hexFormat = intValue <= 0xFF
                ? $"0x{intValue:X2}"
                : intValue <= 0xFFFF
                    ? $"0x{intValue:X4}"
                    : $"0x{intValue:X8}";

            // 缓存格式
            formatCache.TryAdd(intValue, hexFormat);
            return hexFormat;
        }

        // 默认显示为十进制
        string decFormat = intValue.ToString();
        formatCache.TryAdd(intValue, decFormat);
        return decFormat;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return targetType == typeof(int) ? 0 : targetType == typeof(byte) ? (byte)0 : Activator.CreateInstance(targetType)!;
        }

        string input = value.ToString()!.Trim();

        // 只有明确带有 0x 或 0X 前缀的输入才解析为十六进制
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            string hexPart = input.Substring(2);
            if (int.TryParse(hexPart, NumberStyles.HexNumber, culture, out int hexValue))
            {
                // 缓存格式（保持用户输入的格式，包括大小写）
                formatCache.AddOrUpdate(hexValue, input, (k, v) => input);

                if (targetType == typeof(int))
                    return hexValue;
                if (targetType == typeof(byte))
                    return (byte)hexValue;
                return hexValue;
            }
        }

        // 没有 0x 前缀的输入，统一解析为十进制
        if (int.TryParse(input, NumberStyles.Integer, culture, out int decValue))
        {
            // 缓存格式（十进制）
            formatCache.AddOrUpdate(decValue, input, (k, v) => input);

            if (targetType == typeof(int))
                return decValue;
            if (targetType == typeof(byte))
                return (byte)decValue;
            return decValue;
        }

        // 解析失败，返回默认值
        return targetType == typeof(int) ? 0 : targetType == typeof(byte) ? (byte)0 : Activator.CreateInstance(targetType)!;
    }
}