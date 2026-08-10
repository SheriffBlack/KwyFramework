namespace Kwy.ComponentModel;

/// <summary>
/// Specifies the localization resource key used to display a property category.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CategoryKeyAttribute : Attribute
{
    public CategoryKeyAttribute(string key)
    {
        Key = key;
    }

    public string Key { get; }
}
