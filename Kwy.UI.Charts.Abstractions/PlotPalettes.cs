using Kwy.UI.Primitives;

namespace Kwy.UI.Charts.Abstractions;

public static class PlotPalettes
{
    public static readonly Rgba32[] Modern =
    [
        Rgba32.FromRgb(0x34, 0x98, 0xdb),
        Rgba32.FromRgb(0x2e, 0xcc, 0x71),
        Rgba32.FromRgb(0xe7, 0x4c, 0x3c),
        Rgba32.FromRgb(0xf1, 0xc4, 0x0f),
        Rgba32.FromRgb(0x9b, 0x59, 0xb6),
        Rgba32.FromRgb(0x1a, 0xbc, 0x9c),
        Rgba32.FromRgb(0xe6, 0x7e, 0x22),
        Rgba32.FromRgb(0x34, 0x49, 0x5e),
        Rgba32.FromRgb(0xd3, 0x54, 0x00),
        Rgba32.FromRgb(0xc0, 0x39, 0x2b),
        Rgba32.FromRgb(0x16, 0xa0, 0x85),
        Rgba32.FromRgb(0x27, 0xae, 0x60),
        Rgba32.FromRgb(0x29, 0x80, 0xb9),
        Rgba32.FromRgb(0x8e, 0x44, 0xad),
        Rgba32.FromRgb(0xf3, 0x9c, 0x12),
        Rgba32.FromRgb(0x7f, 0x8c, 0x8d)
    ];

    private static readonly Dictionary<string, Rgba32> KeyToColorMap = new(StringComparer.OrdinalIgnoreCase);
    private static int autoColorIndex;

    public static Rgba32 GetColor(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Modern[0];
        }

        lock (KeyToColorMap)
        {
            if (KeyToColorMap.TryGetValue(key, out var color))
            {
                return color;
            }

            color = Modern[autoColorIndex % Modern.Length];
            KeyToColorMap[key] = color;
            autoColorIndex++;
            return color;
        }
    }

    public static Rgba32 GetColor(int index)
    {
        return Modern[Math.Abs(index) % Modern.Length];
    }
}
