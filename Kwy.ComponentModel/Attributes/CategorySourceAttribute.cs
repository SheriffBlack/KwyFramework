namespace Kwy.ComponentModel;

/// <summary>
/// Uses another property on the same source object as the dynamic category name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CategorySourceAttribute : Attribute
{
    public CategorySourceAttribute(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}