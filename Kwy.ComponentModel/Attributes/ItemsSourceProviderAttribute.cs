namespace Kwy.ComponentModel;

/// <summary>
/// 为 ComboBox、RadioButton 或单位选择提供动态候选数据源。
/// ProviderName 指向当前配置对象上的公共属性或无参公共方法。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ItemsSourceProviderAttribute : Attribute
{
    public ItemsSourceProviderAttribute(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ProviderName = providerName;
    }

    public string ProviderName { get; }
}