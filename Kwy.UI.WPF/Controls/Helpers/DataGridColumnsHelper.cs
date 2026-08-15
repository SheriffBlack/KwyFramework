using Kwy.UI.DataGrids;
using Kwy.UI.Enums;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// DataGrid 动态列行为类。
/// 列、行、单元格状态来自 Kwy.UI.DataGrids，WPF helper 只负责渲染。
/// </summary>
public static class DataGridColumnsHelper
{
    private static readonly ConditionalWeakTable<DataGrid, WeakReference<INotifyCollectionChanged>> dataGridToCollectionMap = new();

    private static readonly ConditionalWeakTable<INotifyCollectionChanged, WeakReference<DataGrid>> collectionToDataGridMap = new();

    private static readonly ConditionalWeakTable<DataGrid, object> initializedDataGrids = new();

    public static readonly DependencyProperty ColumnsSourceProperty =
        DependencyProperty.RegisterAttached(
            "ColumnsSource",
            typeof(IEnumerable<IDataGridColumnDescriptor>),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(null, OnColumnsSourceChanged));

    public static void SetColumnsSource(DependencyObject element, IEnumerable<IDataGridColumnDescriptor>? value)
        => element.SetValue(ColumnsSourceProperty, value);

    public static IEnumerable<IDataGridColumnDescriptor>? GetColumnsSource(DependencyObject element)
        => (IEnumerable<IDataGridColumnDescriptor>?)element.GetValue(ColumnsSourceProperty);

    /// <summary>
    /// 行标题列配置。一般可直接把 RowName 放在 ColumnsSource 中，并关闭 ShowRowHeaderColumn。
    /// </summary>
    public static readonly DependencyProperty RowHeaderColumnProperty =
        DependencyProperty.RegisterAttached(
            "RowHeaderColumn",
            typeof(IDataGridColumnDescriptor),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(null, OnColumnOptionsChanged));

    public static void SetRowHeaderColumn(DependencyObject element, IDataGridColumnDescriptor? value)
        => element.SetValue(RowHeaderColumnProperty, value);

    public static IDataGridColumnDescriptor? GetRowHeaderColumn(DependencyObject element)
        => (IDataGridColumnDescriptor?)element.GetValue(RowHeaderColumnProperty);

    public static readonly DependencyProperty ShowRowHeaderColumnProperty =
        DependencyProperty.RegisterAttached(
            "ShowRowHeaderColumn",
            typeof(bool),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(true, OnColumnOptionsChanged));

    public static void SetShowRowHeaderColumn(DependencyObject element, bool value)
        => element.SetValue(ShowRowHeaderColumnProperty, value);

    public static bool GetShowRowHeaderColumn(DependencyObject element)
        => (bool)element.GetValue(ShowRowHeaderColumnProperty);

    public static readonly DependencyProperty DefaultElementStyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "DefaultElementStyleKey",
            typeof(string),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata("DataGridCellTextBlockStyle"));

    public static void SetDefaultElementStyleKey(DependencyObject element, string value)
        => element.SetValue(DefaultElementStyleKeyProperty, value);

    public static string GetDefaultElementStyleKey(DependencyObject element)
        => (string)element.GetValue(DefaultElementStyleKeyProperty);

    private static void OnColumnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        initializedDataGrids.Remove(dataGrid);
        UpdateColumns(dataGrid);
    }

    private static void OnColumnsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= OnColumnsCollectionChanged;
            dataGridToCollectionMap.Remove(dataGrid);
            collectionToDataGridMap.Remove(oldCollection);
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += OnColumnsCollectionChanged;

            dataGridToCollectionMap.Remove(dataGrid);
            dataGridToCollectionMap.Add(dataGrid, new WeakReference<INotifyCollectionChanged>(newCollection));

            collectionToDataGridMap.Remove(newCollection);
            collectionToDataGridMap.Add(newCollection, new WeakReference<DataGrid>(dataGrid));
        }

        initializedDataGrids.Remove(dataGrid);
        UpdateColumns(dataGrid);
    }

    private static void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not INotifyCollectionChanged collection)
        {
            return;
        }

        if (!collectionToDataGridMap.TryGetValue(collection, out WeakReference<DataGrid>? dataGridRef))
        {
            return;
        }

        if (!dataGridRef.TryGetTarget(out DataGrid? dataGrid))
        {
            collectionToDataGridMap.Remove(collection);
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (ReferenceEquals(GetColumnsSource(dataGrid), collection))
                {
                    initializedDataGrids.Remove(dataGrid);
                    UpdateColumns(dataGrid);
                }
            }));
    }

    private static void UpdateColumns(DataGrid dataGrid)
    {
        IEnumerable<IDataGridColumnDescriptor>? columns = GetColumnsSource(dataGrid);
        if (columns == null)
        {
            dataGrid.Columns.Clear();
            return;
        }

        IReadOnlyList<IDataGridColumnDescriptor> columnList = columns as IReadOnlyList<IDataGridColumnDescriptor> ?? columns.ToList();
        int expectedColumnCount = columnList.Count + (GetShowRowHeaderColumn(dataGrid) ? 1 : 0);
        if (initializedDataGrids.TryGetValue(dataGrid, out _) && dataGrid.Columns.Count == expectedColumnCount)
        {
            return;
        }

        dataGrid.Columns.Clear();

        if (GetShowRowHeaderColumn(dataGrid))
        {
            IDataGridColumnDescriptor? rowHeaderColumn = GetRowHeaderColumn(dataGrid);
            dataGrid.Columns.Add(rowHeaderColumn == null ? CreateDefaultRowHeaderColumn(dataGrid) : CreateColumn(dataGrid, rowHeaderColumn));
        }

        foreach (IDataGridColumnDescriptor column in columnList)
        {
            dataGrid.Columns.Add(CreateColumn(dataGrid, column));
        }

        initializedDataGrids.Remove(dataGrid);
        initializedDataGrids.Add(dataGrid, new object());
    }

    private static DataGridColumn CreateDefaultRowHeaderColumn(DataGrid dataGrid)
    {
        var column = new DataGridTextColumn
        {
            Header = string.Empty,
            Width = DataGridLength.Auto,
            IsReadOnly = true,
            Binding = new Binding(nameof(DisplayRowItem.RowName))
        };
        ApplyElementStyle(dataGrid, column, null);
        return column;
    }

    private static DataGridColumn CreateColumn(DataGrid dataGrid, IDataGridColumnDescriptor descriptor)
    {
        WpfDataGridColumnOptions options = descriptor as WpfDataGridColumnOptions ?? WpfDataGridColumnOptions.Default;
        DataGridColumn column = options.ColumnType switch
        {
            DataGridColumnType.CheckBox => new DataGridCheckBoxColumn(),
            DataGridColumnType.ComboBox => new DataGridComboBoxColumn(),
            DataGridColumnType.Template => new DataGridTemplateColumn(),
            _ => new DataGridTextColumn()
        };

        column.Header = descriptor.DisplayName;
        column.Width = options.Width;
        column.IsReadOnly = options.IsReadOnly;
        column.CanUserSort = options.CanUserSort;
        column.CanUserResize = options.CanUserResize;
        column.CanUserReorder = options.CanUserReorder;

        var binding = new Binding(CreateBindingPath(descriptor))
        {
            Mode = options.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            Converter = options.Converter,
            ConverterParameter = options.ConverterParameter,
            StringFormat = options.StringFormat
        };

        if (!string.Equals(descriptor.ParameterId, nameof(DisplayRowItem.RowName), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(descriptor.ParameterId))
        {
            Style cellStyle = CreateDynamicCellStyle(dataGrid, descriptor.ParameterId);
            cellStyle.Seal();
            column.CellStyle = cellStyle;
        }

        switch (column)
        {
            case DataGridTextColumn textColumn:
                textColumn.Binding = binding;
                ApplyElementStyle(dataGrid, textColumn, options);
                ApplyEditingElementStyle(textColumn, options);
                break;

            case DataGridCheckBoxColumn checkBoxColumn:
                checkBoxColumn.Binding = binding;
                break;

            case DataGridComboBoxColumn comboBoxColumn:
                comboBoxColumn.SelectedItemBinding = binding;
                break;

            case DataGridTemplateColumn templateColumn when !string.IsNullOrWhiteSpace(options.CellTemplateKey):
                templateColumn.CellTemplate = dataGrid.TryFindResource(options.CellTemplateKey) as DataTemplate;
                break;
        }

        return column;
    }
    private static Style CreateDynamicCellStyle(DataGrid dataGrid, string parameterId)
    {
        var cellStyle = new Style(typeof(DataGridCell), ResolveBaseCellStyle(dataGrid));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(
            DataGridCell.BackgroundProperty,
            new Binding($"Item[{parameterId}].Judge")
            {
                Converter = Kwy.UI.WPF.Converters.JudgeToBrushConverter.Instance
            }));
        return cellStyle;
    }

    private static Style? ResolveBaseCellStyle(DataGrid dataGrid)
    {
        if (dataGrid.CellStyle != null)
        {
            return dataGrid.CellStyle;
        }

        return dataGrid.TryFindResource("ModernDataGridCellStyle") as Style
            ?? Application.Current.TryFindResource("ModernDataGridCellStyle") as Style;
    }
    private static string CreateBindingPath(IDataGridColumnDescriptor descriptor)
    {
        if (descriptor is WpfDataGridColumnOptions options && !string.IsNullOrWhiteSpace(options.BindingPath))
        {
            return options.BindingPath;
        }

        return string.Equals(descriptor.ParameterId, nameof(DisplayRowItem.RowName), StringComparison.OrdinalIgnoreCase)
            ? nameof(DisplayRowItem.RowName)
            : $"Item[{descriptor.ParameterId}].Value";
    }

    private static void ApplyElementStyle(DataGrid dataGrid, DataGridTextColumn textColumn, WpfDataGridColumnOptions? options)
    {
        if (options?.ElementStyle != null)
        {
            textColumn.ElementStyle = options.ElementStyle;
            return;
        }

        string? styleKey = options?.ElementStyleKey ?? GetDefaultElementStyleKey(dataGrid);
        if (!string.IsNullOrEmpty(styleKey)
            && (dataGrid.TryFindResource(styleKey) ?? Application.Current.TryFindResource(styleKey)) is Style style)
        {
            textColumn.ElementStyle = style;
            return;
        }

        if (options?.HorizontalAlignment != null)
        {
            var alignmentStyle = new Style(typeof(TextBlock));
            alignmentStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, options.HorizontalAlignment.Value));
            textColumn.ElementStyle = alignmentStyle;
        }
    }

    private static void ApplyEditingElementStyle(DataGridTextColumn textColumn, WpfDataGridColumnOptions options)
    {
        if (options.EditingElementStyle != null)
        {
            textColumn.EditingElementStyle = options.EditingElementStyle;
            return;
        }

        if (!string.IsNullOrEmpty(options.EditingElementStyleKey)
            && Application.Current.TryFindResource(options.EditingElementStyleKey) is Style style)
        {
            textColumn.EditingElementStyle = style;
        }
    }
}


