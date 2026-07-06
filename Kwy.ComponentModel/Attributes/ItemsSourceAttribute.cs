namespace Kwy.ComponentModel;

/// <summary>
/// 为 ComboBox 或 RadioButton 提供候选数据源（静态简单列表）
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ItemsSourceAttribute : Attribute
{
    public string[] Items { get; }
    public ItemsSourceAttribute(params string[] items) => Items = items;
}
