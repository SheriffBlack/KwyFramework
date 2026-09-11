using Kwy.UI.DataGrids;
using Kwy.UI.Enums;
using System.Collections.Specialized;
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
    private static readonly DependencyProperty SubscribedCollectionProperty =
        DependencyProperty.RegisterAttached(
            "SubscribedCollection",
            typeof(INotifyCollectionChanged),
            typeof(DataGridColumnsHelper));

    private static readonly DependencyProperty ColumnsUpdatePendingProperty =
        DependencyProperty.RegisterAttached(
            "ColumnsUpdatePending",
            typeof(bool),
            typeof(DataGridColumnsHelper));

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
    /// 行标题列配置。也可直接将标题列放入 ColumnsSource，并关闭 ShowRowHeaderColumn。
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
            new PropertyMetadata(false, OnColumnOptionsChanged));

    public static void SetShowRowHeaderColumn(DependencyObject element, bool value)
        => element.SetValue(ShowRowHeaderColumnProperty, value);

    public static bool GetShowRowHeaderColumn(DependencyObject element)
        => (bool)element.GetValue(ShowRowHeaderColumnProperty);

    public static readonly DependencyProperty RowHeaderBindingPathProperty =
        DependencyProperty.RegisterAttached(
            "RowHeaderBindingPath",
            typeof(string),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(".", OnColumnOptionsChanged));

    public static void SetRowHeaderBindingPath(DependencyObject element, string value)
        => element.SetValue(RowHeaderBindingPathProperty, value);

    public static string GetRowHeaderBindingPath(DependencyObject element)
        => (string)element.GetValue(RowHeaderBindingPathProperty);

    public static readonly DependencyProperty DefaultElementStyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "DefaultElementStyleKey",
            typeof(object),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(KwyResourceKeys.DataGridCellTextBlockStyle));

    public static void SetDefaultElementStyleKey(DependencyObject element, object value)
        => element.SetValue(DefaultElementStyleKeyProperty, value);

    public static object GetDefaultElementStyleKey(DependencyObject element)
        => element.GetValue(DefaultElementStyleKeyProperty);

    private static void OnColumnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        UpdateColumns(dataGrid);
    }

    private static void OnColumnsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        if (dataGrid.GetValue(SubscribedCollectionProperty) is INotifyCollectionChanged oldCollection)
        {
            CollectionChangedEventManager.RemoveHandler(oldCollection, dataGrid.OnColumnsCollectionChanged);
            dataGrid.ClearValue(SubscribedCollectionProperty);
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            CollectionChangedEventManager.AddHandler(newCollection, dataGrid.OnColumnsCollectionChanged);
            dataGrid.SetValue(SubscribedCollectionProperty, newCollection);
        }

        UpdateColumns(dataGrid);
    }

    private static void OnColumnsCollectionChanged(this DataGrid dataGrid, object? sender, NotifyCollectionChangedEventArgs e)
    {
        if ((bool)dataGrid.GetValue(ColumnsUpdatePendingProperty))
        {
            return;
        }

        dataGrid.SetValue(ColumnsUpdatePendingProperty, true);
        dataGrid.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.DataBind,
            new Action(() =>
            {
                dataGrid.SetValue(ColumnsUpdatePendingProperty, false);
                if (ReferenceEquals(GetColumnsSource(dataGrid), sender))
                {
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
    }

    private static DataGridColumn CreateDefaultRowHeaderColumn(DataGrid dataGrid)
    {
        var column = new DataGridTextColumn
        {
            Header = string.Empty,
            Width = DataGridLength.Auto,
            IsReadOnly = true,
            Binding = new Binding(GetRowHeaderBindingPath(dataGrid))
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

        column.Header = descriptor.Header;
        column.Width = options.Width;
        column.IsReadOnly = options.IsReadOnly;
        column.CanUserSort = options.CanUserSort;
        column.CanUserResize = options.CanUserResize;
        column.CanUserReorder = options.CanUserReorder;

        var binding = new Binding(descriptor.BindingPath)
        {
            Mode = options.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            Converter = options.Converter,
            ConverterParameter = options.ConverterParameter,
            StringFormat = options.StringFormat
        };

        if (!string.IsNullOrWhiteSpace(descriptor.Key))
        {
            Style cellStyle = CreateDynamicCellStyle(dataGrid, descriptor.Key);
            cellStyle.Seal();
            column.CellStyle = cellStyle;
        }

        switch (column)
        {
            case DataGridTextColumn textColumn:
                textColumn.Binding = binding;
                ApplyElementStyle(dataGrid, textColumn, options);
                ApplyEditingElementStyle(dataGrid, textColumn, options);
                break;

            case DataGridCheckBoxColumn checkBoxColumn:
                checkBoxColumn.Binding = binding;
                break;

            case DataGridComboBoxColumn comboBoxColumn:
                comboBoxColumn.SelectedItemBinding = binding;
                break;

            case DataGridTemplateColumn templateColumn when options.CellTemplateKey != null:
                templateColumn.CellTemplate = dataGrid.TryFindResource(options.CellTemplateKey) as DataTemplate;
                break;
        }

        return column;
    }
    private static Style CreateDynamicCellStyle(DataGrid dataGrid, string cellKey)
    {
        var cellStyle = new Style(typeof(DataGridCell), ResolveBaseCellStyle(dataGrid));
        cellStyle.Setters.Add(new Setter(DataGridCell.TagProperty, new Binding($"[{cellKey}].VisualState")));
        return cellStyle;
    }

    private static Style? ResolveBaseCellStyle(DataGrid dataGrid)
    {
        if (dataGrid.CellStyle != null)
        {
            return dataGrid.CellStyle;
        }

        return dataGrid.TryFindResource(KwyResourceKeys.DynamicValidationDataGridCellStyle) as Style
            ?? Application.Current?.TryFindResource(KwyResourceKeys.DynamicValidationDataGridCellStyle) as Style
            ?? dataGrid.TryFindResource(KwyResourceKeys.ModernDataGridCellStyle) as Style
            ?? Application.Current?.TryFindResource(KwyResourceKeys.ModernDataGridCellStyle) as Style;
    }
    private static void ApplyElementStyle(DataGrid dataGrid, DataGridTextColumn textColumn, WpfDataGridColumnOptions? options)
    {
        if (options?.ElementStyle != null)
        {
            textColumn.ElementStyle = options.ElementStyle;
            return;
        }

        object? styleKey = options?.ElementStyleKey ?? GetDefaultElementStyleKey(dataGrid);
        if (styleKey != null
            && (dataGrid.TryFindResource(styleKey) ?? Application.Current?.TryFindResource(styleKey)) is Style style)
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

    private static void ApplyEditingElementStyle(DataGrid dataGrid, DataGridTextColumn textColumn, WpfDataGridColumnOptions options)
    {
        if (options.EditingElementStyle != null)
        {
            textColumn.EditingElementStyle = options.EditingElementStyle;
            return;
        }

        if (options.EditingElementStyleKey != null
            && (dataGrid.TryFindResource(options.EditingElementStyleKey)
                ?? Application.Current?.TryFindResource(options.EditingElementStyleKey)) is Style style)
        {
            textColumn.EditingElementStyle = style;
        }
    }
}


