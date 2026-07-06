namespace Kwy.ComponentModel;

[AttributeUsage(AttributeTargets.Property)]
public class GroupWidthAttribute : Attribute
{
    // 1.0 代表占满 100% 宽度（独占一行）
    // 0.5 代表占满 50% 宽度（刚好并排成田字）
    public double WidthRatio { get; }

    public GroupWidthAttribute(double widthRatio)
    {
        WidthRatio = widthRatio;
    }
}