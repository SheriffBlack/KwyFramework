namespace Kwy.Communicate.Secs;

public sealed record SecsItem(SecsItemFormat Format, object? Value)
{
    public static SecsItem L(params SecsItem[] items) => new(SecsItemFormat.List, items);

    public static SecsItem A(string value) => new(SecsItemFormat.Ascii, value);

    public static SecsItem B(params byte[] value) => new(SecsItemFormat.Binary, value);

    public static SecsItem Bool(params bool[] value) => new(SecsItemFormat.Boolean, value);

    public static SecsItem I1(params sbyte[] value) => new(SecsItemFormat.Int1, value);

    public static SecsItem I2(params short[] value) => new(SecsItemFormat.Int2, value);

    public static SecsItem I4(params int[] value) => new(SecsItemFormat.Int4, value);

    public static SecsItem U1(params byte[] value) => new(SecsItemFormat.UInt1, value);

    public static SecsItem U2(params ushort[] value) => new(SecsItemFormat.UInt2, value);

    public static SecsItem U4(params uint[] value) => new(SecsItemFormat.UInt4, value);

    public static SecsItem F4(params float[] value) => new(SecsItemFormat.Float4, value);

    public static SecsItem F8(params double[] value) => new(SecsItemFormat.Float8, value);

    public IReadOnlyList<SecsItem> Children
        => Format == SecsItemFormat.List && Value is SecsItem[] items ? items : Array.Empty<SecsItem>();
}
