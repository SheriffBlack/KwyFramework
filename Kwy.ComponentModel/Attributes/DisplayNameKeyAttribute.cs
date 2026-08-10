namespace Kwy.ComponentModel;

/// <summary>
/// Specifies the localization resource key used to display a property name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DisplayNameKeyAttribute : Attribute
{
    public DisplayNameKeyAttribute(string key)
    {
        Key = key;
    }

    public string Key { get; }
}
