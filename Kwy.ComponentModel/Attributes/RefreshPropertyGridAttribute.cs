namespace Kwy.ComponentModel;

/// <summary>
/// Marks a property whose value change requires rebuilding the generated property grid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RefreshPropertyGridAttribute : Attribute
{
}