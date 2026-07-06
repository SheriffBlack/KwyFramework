using Kwy.UI.WPF.Controls.Helpers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// ComboBox 图标转换器
/// 优先从 SelectedItem 获取图标，如果没有则使用 ComboBox 自身的图标
/// </summary>
public class ComboBoxIconConverter : MarkupExtension, IMultiValueConverter
{
    private static ComboBoxIconConverter? instance;
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> propertyCache = new();

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3)
        {
            return null;
        }

        // values[0]: SelectedItem (可能是 ComboBoxItem 或数据对象)
        // values[1]: ComboBox 自身的 Icon
        // values[2]: ComboBox 本身

        // 优先从 SelectedItem 获取图标
        var selectedItem = values[0];
        var comboBox = values[2] as ComboBox;

        if (selectedItem != null && selectedItem != DependencyProperty.UnsetValue)
        {
            DependencyObject? container = null;

            // 如果 selectedItem 本身就是 ComboBoxItem
            if (selectedItem is ComboBoxItem comboBoxItem)
            {
                container = comboBoxItem;
            }
            // 如果 selectedItem 是数据对象，尝试从 ItemContainerGenerator 查找对应的 ComboBoxItem
            else if (comboBox != null)
            {
                container = comboBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ComboBoxItem;
            }

            if (container != null)
            {
                var icon = IconHelper.GetIcon(container);
                if (icon != null && icon != DependencyProperty.UnsetValue)
                {
                    return icon;
                }
            }

            // 如果找不到容器（容器可能还没生成），尝试从数据对象直接获取 Icon 属性
            if (selectedItem != null && !(selectedItem is ComboBoxItem))
            {
                // 🌟 性能优化：通过缓存获取 Icon 属性
                var type = selectedItem.GetType();
                var iconProperty = propertyCache.GetOrAdd(type, t => t.GetProperty("Icon"));

                if (iconProperty != null)
                {
                    var iconValue = iconProperty.GetValue(selectedItem);
                    if (iconValue != null)
                    {
                        // 如果 Icon 是字符串，需要通过 IconKeyToResourceConverter 转换
                        if (iconValue is string iconKey && !string.IsNullOrEmpty(iconKey))
                        {
                            // 尝试从应用程序资源中查找图标资源
                            try
                            {
                                if (Application.Current != null && Application.Current.Resources.Contains(iconKey))
                                {
                                    return Application.Current.Resources[iconKey];
                                }
                            }
                            catch
                            {
                                // 忽略资源查找异常
                            }
                        }
                        return iconValue;
                    }
                }
            }
        }

        // 如果没有选中项或选中项没有图标，则使用 ComboBox 自身的图标
        var comboBoxIcon = values[1];
        if (comboBoxIcon == DependencyProperty.UnsetValue)
        {
            return null;
        }
        return comboBoxIcon;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return instance ??= new ComboBoxIconConverter();
    }
}