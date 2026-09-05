namespace Kwy.ComponentModel;

/// <summary>
/// Displays a property in the property grid only when the named boolean property has the expected value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VisibleWhenAttribute : Attribute
{
    public VisibleWhenAttribute(string propertyName, bool expectedValue = true)
    {
        PropertyName = propertyName;
        ExpectedValue = expectedValue;
    }

    public string PropertyName { get; }

    public bool ExpectedValue { get; }
}
