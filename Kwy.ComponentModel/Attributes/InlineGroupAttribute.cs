namespace Kwy.ComponentModel;

/// <summary>
/// Marks properties that should be displayed inline by metadata-driven editors.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InlineGroupAttribute : Attribute
{
    public InlineGroupAttribute(string groupName)
    {
        GroupName = groupName ?? string.Empty;
    }

    public string GroupName { get; }
}