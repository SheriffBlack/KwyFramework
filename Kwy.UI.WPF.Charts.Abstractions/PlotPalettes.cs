using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.Abstractions;

public static class PlotPalettes
{
    public static readonly Color[] Modern =
    [
        Color.FromRgb(0x34, 0x98, 0xdb),
        Color.FromRgb(0x2e, 0xcc, 0x71),
        Color.FromRgb(0xe7, 0x4c, 0x3c),
        Color.FromRgb(0xf1, 0xc4, 0x0f),
        Color.FromRgb(0x9b, 0x59, 0xb6),
        Color.FromRgb(0x1a, 0xbc, 0x9c),
        Color.FromRgb(0xe6, 0x7e, 0x22),
        Color.FromRgb(0x34, 0x49, 0x5e),
        Color.FromRgb(0xd3, 0x54, 0x00),
        Color.FromRgb(0xc0, 0x39, 0x2b),
        Color.FromRgb(0x16, 0xa0, 0x85),
        Color.FromRgb(0x27, 0xae, 0x60),
        Color.FromRgb(0x29, 0x80, 0xb9),
        Color.FromRgb(0x8e, 0x44, 0xad),
        Color.FromRgb(0xf3, 0x9c, 0x12),
        Color.FromRgb(0x7f, 0x8c, 0x8d)
    ];

    private static readonly Dictionary<string, Color> KeyToColorMap = new(StringComparer.OrdinalIgnoreCase);
    private static int autoColorIndex;

    public static Color GetColor(string key)
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

    public static Color GetColor(int index)
    {
        return Modern[Math.Abs(index) % Modern.Length];
    }
}
