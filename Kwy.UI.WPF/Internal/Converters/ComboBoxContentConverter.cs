using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Kwy.UI.WPF.Internal.Converters;

/// <summary>
/// ComboBox 内容转换器
/// 从 SelectedItem 获取对应的 ComboBoxItem 的 Content
/// </summary>
internal sealed class ComboBoxContentConverter : MarkupExtension, IMultiValueConverter
{
    private static ComboBoxContentConverter? instance;
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> propertyCache = new();

    private static object? GetCachedPropertyValue(object obj, string propertyName)
    {
        var type = obj.GetType();
        var prop = propertyCache.GetOrAdd((type, propertyName), key => key.Item1.GetProperty(key.Item2));
        return prop?.GetValue(obj);
    }

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3)
        {
            return null;
        }

        var selectedItem = values[0];
        var comboBox = values[1] as ComboBox;

        if (selectedItem == null || selectedItem == DependencyProperty.UnsetValue || comboBox == null)
        {
            return null;
        }

        ComboBoxItem? container = null;

        // 如果 selectedItem 本身就是 ComboBoxItem
        if (selectedItem is ComboBoxItem comboBoxItem)
        {
            container = comboBoxItem;
        }
        // 如果 selectedItem 是数据对象，尝试从 ItemContainerGenerator 查找对应的 ComboBoxItem
        else
        {
            // 首先尝试使用传入的 selectedItem
            container = comboBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ComboBoxItem;

            // 如果找不到，尝试使用 SelectedItem（可能是不同的引用）
            if (container == null && comboBox.SelectedItem != null && comboBox.SelectedItem != selectedItem)
            {
                container = comboBox.ItemContainerGenerator.ContainerFromItem(comboBox.SelectedItem) as ComboBoxItem;
            }
        }

        // 如果找到了容器，返回其 Content
        if (container != null)
        {
            return container.Content;
        }

        if (values[2] is string displayMemberPath && !string.IsNullOrWhiteSpace(displayMemberPath))
        {
            return GetCachedPropertyValue(selectedItem, displayMemberPath);
        }

        return selectedItem.ToString();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return targetTypes.Select(static _ => Binding.DoNothing).ToArray();
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return instance ??= new ComboBoxContentConverter();
    }
}
