namespace Kwy.UI.ThreeD.Abstractions.Primitives;

/// <summary>
/// 线性 RGBA 颜色，各分量通常位于 0 到 1 之间。
/// </summary>
public readonly record struct LinearColor(float Red, float Green, float Blue, float Alpha = 1F)
{
    public static LinearColor Transparent => new(0F, 0F, 0F, 0F);

    public static LinearColor Black => new(0F, 0F, 0F);

    public static LinearColor White => new(1F, 1F, 1F);

    public LinearColor Clamp() => new(
        Math.Clamp(Red, 0F, 1F),
        Math.Clamp(Green, 0F, 1F),
        Math.Clamp(Blue, 0F, 1F),
        Math.Clamp(Alpha, 0F, 1F));

    public static LinearColor FromBytes(byte red, byte green, byte blue, byte alpha = byte.MaxValue) =>
        new(red / 255F, green / 255F, blue / 255F, alpha / 255F);
}
