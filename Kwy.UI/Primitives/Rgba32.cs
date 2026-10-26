namespace Kwy.UI.Primitives;

/// <summary>
/// 表示 sRGB 颜色空间中的一个不可变的 32 位颜色。
/// </summary>
public readonly record struct Rgba32(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue)
{
    public static Rgba32 Transparent => new(0, 0, 0, 0);

    public static Rgba32 Black => new(0, 0, 0);

    public static Rgba32 White => new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public static Rgba32 FromRgb(byte red, byte green, byte blue) => new(red, green, blue);

    public static Rgba32 FromArgb(byte alpha, byte red, byte green, byte blue) => new(red, green, blue, alpha);

    public uint ToPackedRgba() =>
        ((uint)Red << 24) | ((uint)Green << 16) | ((uint)Blue << 8) | Alpha;

    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}";
}
