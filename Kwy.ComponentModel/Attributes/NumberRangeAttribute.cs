namespace Kwy.ComponentModel;

/// <summary>
/// Declares numeric editor hints for reflected property metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NumberRangeAttribute : Attribute
{
    public NumberRangeAttribute(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double SmallChange { get; set; } = double.NaN;

    public int DecimalPlaces { get; set; } = -1;
}
