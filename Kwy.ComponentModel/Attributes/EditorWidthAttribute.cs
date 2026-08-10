namespace Kwy.ComponentModel;

/// <summary>
/// 指定属性编辑器的输入控件宽度。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EditorWidthAttribute : Attribute
{
    public EditorWidthAttribute(double width)
    {
        Width = width;
    }

    public double Width { get; }
}