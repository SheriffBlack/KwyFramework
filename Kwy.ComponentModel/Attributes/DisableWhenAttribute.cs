namespace Kwy.ComponentModel;

/// <summary>
/// Disables a property-grid editor when the named boolean property has the expected value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DisableWhenAttribute : Attribute
{
    public DisableWhenAttribute(string propertyName, bool expectedValue = true)
    {
        PropertyName = propertyName;
        ExpectedValue = expectedValue;
    }

    public string PropertyName { get; }

    public bool ExpectedValue { get; }
}
