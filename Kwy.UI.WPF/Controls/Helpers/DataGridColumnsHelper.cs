using Kwy.UI.Enums;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// DataGrid 动态列行为类
/// 根据参数列描述自动生成列，支持集合变化监听（优化版）
/// </summary>
public static class DataGridColumnsHelper
{
    // 使用 WeakReference 存储 DataGrid 和集合的关联关系，避免内存泄漏
    private static readonly ConditionalWeakTable<DataGrid, WeakReference<INotifyCollectionChanged>> dataGridToCollectionMap = new();

    private static readonly ConditionalWeakTable<INotifyCollectionChanged, WeakReference<DataGrid>> collectionToDataGridMap = new();

    #region 数据结构定义

    /// <summary>
    /// DataGrid 列描述（UI 抽象）
    /// </summary>
    public interface IDataGridColumnDescriptor
    {
        /// <summary>
        /// 列头显示文本
        /// </summary>
        string Header { get; }

        /// <summary>
        /// 绑定路径（完整 Binding.Path）
        /// 例如：Cells[R] 或 PropertyName
        /// </summary>
        string BindingPath { get; }

        /// <summary>
        /// 列宽
        /// </summary>
        DataGridLength Width { get; }

        /// <summary>
        /// 列类型（可选，默认 TextColumn）
        /// </summary>
        DataGridColumnType ColumnType { get; }

        /// <summary>
        /// 是否只读（可选，默认 true）
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// 水平对齐方式（可选）
        /// </summary>
        TextAlignment? HorizontalAlignment { get; }

        /// <summary>
        /// 值转换器（可选）
        /// </summary>
        IValueConverter? Converter { get; }

        /// <summary>
        /// 转换器参数（可选）
        /// </summary>
        object? ConverterParameter { get; }

        /// <summary>
        /// 字符串格式（可选，用于 StringFormat）
        /// </summary>
        string? StringFormat { get; }

        /// <summary>
        /// 是否可排序（可选，默认 true）
        /// </summary>
        bool CanUserSort { get; }

        /// <summary>
        /// 是否可调整大小（可选，默认 true）
        /// </summary>
        bool CanUserResize { get; }

        /// <summary>
        /// 是否可重新排序（可选，默认 true）
        /// </summary>
        bool CanUserReorder { get; }

        /// <summary>
        /// ElementStyle 样式键（可选，用于 TextColumn 的 ElementStyle）
        /// </summary>
        string? ElementStyleKey { get; }

        /// <summary>
        /// ElementStyle 样式对象（可选，优先级高于 ElementStyleKey）
        /// </summary>
        Style? ElementStyle { get; }

        /// <summary>
        /// EditingElementStyle 样式键（可选，用于编辑时的样式）
        /// </summary>
        string? EditingElementStyleKey { get; }

        /// <summary>
        /// EditingElementStyle 样式对象（可选）
        /// </summary>
        Style? EditingElementStyle { get; }
    }

    /// <summary>
    /// DataGrid 列描述符的默认实现
    /// </summary>
    public class DataGridColumnDescriptor : IDataGridColumnDescriptor
    {
        /// <summary>
        /// 绑定路径（完整 Binding.Path）
        /// 使用索引器属性 Item[key] 而不是 Cells[key]，以支持属性变化通知
        /// </summary>
        // 🌟 核心修改：绑定的路径变成了 Item[key].Value
        public string BindingPath => $"Item[{ParameterId}].Value";

        /// <summary>
        /// 参数ID（用于生成 BindingPath）
        /// </summary>
        public string ParameterId { get; set; } = "";

        /// <summary>CreateColumn
        /// 显示名称（用于 Header）
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 列头显示文本
        /// </summary>
        public string Header => DisplayName;

        /// <summary>
        /// 列宽
        /// </summary>
        public DataGridLength Width { get; set; } = new DataGridLength(1, DataGridLengthUnitType.Star);

        /// <summary>
        /// 列类型
        /// </summary>
        public DataGridColumnType ColumnType { get; set; } = DataGridColumnType.Text;

        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly { get; set; } = true;

        /// <summary>
        /// 水平对齐方式
        /// </summary>
        public TextAlignment? HorizontalAlignment { get; set; }

        /// <summary>
        /// 值转换器
        /// </summary>
        public IValueConverter? Converter { get; set; }

        /// <summary>
        /// 转换器参数
        /// </summary>
        public object? ConverterParameter { get; set; }

        /// <summary>
        /// 字符串格式
        /// </summary>
        public string? StringFormat { get; set; }

        /// <summary>
        /// 是否可排序
        /// </summary>
        public bool CanUserSort { get; set; } = true;

        /// <summary>
        /// 是否可调整大小
        /// </summary>
        public bool CanUserResize { get; set; } = true;

        /// <summary>
        /// 是否可重新排序
        /// </summary>
        public bool CanUserReorder { get; set; } = true;

        /// <summary>
        /// ElementStyle 样式键（可选，用于 TextColumn 的 ElementStyle）
        /// </summary>
        public string? ElementStyleKey { get; set; } = "DataGridCellTextBlockStyle";

        /// <summary>
        /// ElementStyle 样式对象（可选，优先级高于 ElementStyleKey）
        /// </summary>
        public Style? ElementStyle { get; set; }

        /// <summary>
        /// EditingElementStyle 样式键（可选，用于编辑时的样式）
        /// </summary>
        public string? EditingElementStyleKey { get; set; }

        /// <summary>
        /// EditingElementStyle 样式对象（可选）
        /// </summary>
        public Style? EditingElementStyle { get; set; }
    }


    // 🌟 新增：单元格状态包装器，支持细粒度的 INPC 通知
    public class CellState : INotifyPropertyChanged
    {
        private object? _value;
        public object? Value
        {
            get => _value;
            set { if (!Equals(_value, value)) { _value = value; OnPropertyChanged(nameof(Value)); } }
        }

        private bool? _judge;
        public bool? Judge
        {
            get => _judge;
            set { if (_judge != value) { _judge = value; OnPropertyChanged(nameof(Judge)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }



    /// <summary>
    /// DataGrid 中的一行（UI 语义）
    /// </summary>
    public class DisplayRowItem : INotifyPropertyChanged
    {
        private string rowName = "";
        public string RowName
        {
            get => rowName;
            set { if (rowName != value) { rowName = value; OnPropertyChanged(nameof(RowName)); } }
        }

        // 字典的值从 object? 变成了 CellState
        public Dictionary<string, CellState> Cells { get; } = new();

        // 索引器直接返回 CellState
        public CellState this[string key]
        {
            get
            {
                if (!Cells.TryGetValue(key, out var state))
                {
                    state = new CellState();
                    Cells[key] = state;
                }
                return state;
            }
        }

        public static DisplayRowItem CreateRow(object? rowName, params (string key, object? value)[] values)
        {
            var row = new DisplayRowItem { RowName = rowName?.ToString() ?? string.Empty };
            foreach (var (key, value) in values) row.UpdateCell(key, value);
            return row;
        }

        public void UpdateCell(string key, object? value) => this[key].Value = value;

        // 🌟 新增：专门更新判定结果的方法
        public void UpdateJudge(string key, bool? judge) => this[key].Judge = judge;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion 数据结构定义

    // 存储已初始化的 DataGrid，避免重复创建列
    private static readonly ConditionalWeakTable<DataGrid, object> initializedDataGrids = new();

    #region ColumnsSource AttachedProperty

    public static readonly DependencyProperty ColumnsSourceProperty =
        DependencyProperty.RegisterAttached(
            "ColumnsSource",
            typeof(IEnumerable<IDataGridColumnDescriptor>),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(null, OnColumnsSourceChanged));

    public static void SetColumnsSource(
        DependencyObject element,
        IEnumerable<IDataGridColumnDescriptor>? value)
    {
        element.SetValue(ColumnsSourceProperty, value);
    }

    public static IEnumerable<IDataGridColumnDescriptor>? GetColumnsSource(
        DependencyObject element)
    {
        return (IEnumerable<IDataGridColumnDescriptor>?)
            element.GetValue(ColumnsSourceProperty);
    }

    #endregion ColumnsSource AttachedProperty

    #region RowHeaderColumn AttachedProperty

    /// <summary>
    /// 行标题列配置（可选，如果不设置则使用默认配置）
    /// </summary>
    public static readonly DependencyProperty RowHeaderColumnProperty =
        DependencyProperty.RegisterAttached(
            "RowHeaderColumn",
            typeof(IDataGridColumnDescriptor),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(null));

    public static void SetRowHeaderColumn(
        DependencyObject element,
        IDataGridColumnDescriptor? value)
    {
        element.SetValue(RowHeaderColumnProperty, value);
    }

    public static IDataGridColumnDescriptor? GetRowHeaderColumn(
        DependencyObject element)
    {
        return (IDataGridColumnDescriptor?)element.GetValue(RowHeaderColumnProperty);
    }

    #endregion RowHeaderColumn AttachedProperty

    #region ShowRowHeaderColumn AttachedProperty

    /// <summary>
    /// 是否显示行标题列（默认 true）
    /// </summary>
    public static readonly DependencyProperty ShowRowHeaderColumnProperty =
        DependencyProperty.RegisterAttached(
            "ShowRowHeaderColumn",
            typeof(bool),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata(true));

    public static void SetShowRowHeaderColumn(
        DependencyObject element,
        bool value)
    {
        element.SetValue(ShowRowHeaderColumnProperty, value);
    }

    public static bool GetShowRowHeaderColumn(
        DependencyObject element)
    {
        return (bool)element.GetValue(ShowRowHeaderColumnProperty);
    }

    #endregion ShowRowHeaderColumn AttachedProperty

    #region DefaultElementStyleKey AttachedProperty

    /// <summary>
    /// 默认 ElementStyle 样式键（用于所有列，包括行标题列）
    /// </summary>
    public static readonly DependencyProperty DefaultElementStyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "DefaultElementStyleKey",
            typeof(string),
            typeof(DataGridColumnsHelper),
            new PropertyMetadata("DataGridCellTextBlockStyle"));

    public static void SetDefaultElementStyleKey(
        DependencyObject element,
        string value)
    {
        element.SetValue(DefaultElementStyleKeyProperty, value);
    }

    public static string GetDefaultElementStyleKey(
        DependencyObject element)
    {
        return (string)element.GetValue(DefaultElementStyleKeyProperty);
    }

    #endregion DefaultElementStyleKey AttachedProperty

    private static void OnColumnsSourceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
            return;

        // 取消旧的集合变化监听
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= OnColumnsCollectionChanged;

            // 清理关联关系
            if (dataGridToCollectionMap.TryGetValue(dataGrid, out var oldRef))
            {
                dataGridToCollectionMap.Remove(dataGrid);
            }
            if (collectionToDataGridMap.TryGetValue(oldCollection, out var oldDataGridRef))
            {
                collectionToDataGridMap.Remove(oldCollection);
            }
        }

        // 添加新的集合变化监听
        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += OnColumnsCollectionChanged;

            // 建立关联关系（先移除可能存在的旧条目，ConditionalWeakTable.Add 不允许重复 key）
            if (dataGridToCollectionMap.TryGetValue(dataGrid, out _))
                dataGridToCollectionMap.Remove(dataGrid);
            dataGridToCollectionMap.Add(dataGrid, new WeakReference<INotifyCollectionChanged>(newCollection));

            if (collectionToDataGridMap.TryGetValue(newCollection, out _))
                collectionToDataGridMap.Remove(newCollection);
            collectionToDataGridMap.Add(newCollection, new WeakReference<DataGrid>(dataGrid));
        }

        // 更新列（只在首次设置或集合引用变化时更新）
        UpdateColumns(dataGrid);
    }

    private static void OnColumnsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        // 使用 WeakReference 快速找到对应的 DataGrid，避免遍历所有窗口
        if (sender is INotifyCollectionChanged collection)
        {
            if (collectionToDataGridMap.TryGetValue(collection, out var dataGridRef))
            {
                if (dataGridRef.TryGetTarget(out var dataGrid))
                {
                    // 使用 BeginInvoke 避免在集合变化事件中直接操作 UI
                    Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Loaded,
                        new Action(() =>
                        {
                            // 再次验证 DataGrid 仍然有效且关联的集合未变
                            if (dataGrid != null &&
                                ReferenceEquals(GetColumnsSource(dataGrid), collection))
                            {
                                UpdateColumns(dataGrid);
                            }
                        }));
                }
                else
                {
                    // DataGrid 已被回收，清理关联关系
                    collectionToDataGridMap.Remove(collection);
                }
            }
        }
    }

    private static void UpdateColumns(DataGrid dataGrid)
    {
        var columns = GetColumnsSource(dataGrid);
        if (columns == null)
        {
            // 如果列集合为 null，清空列
            if (dataGrid.Columns.Count > 0)
            {
                dataGrid.Columns.Clear();
            }
            return;
        }

        // 检查列是否已经初始化且集合未变化
        // 如果列数量匹配，跳过更新（列集合基本不会变，避免重复创建）
        if (initializedDataGrids.TryGetValue(dataGrid, out _))
        {
            // 检查列数量是否匹配（简单优化：如果列数相同，假设未变化）
            var columnList = columns as IList<IDataGridColumnDescriptor> ?? columns.ToList();
            var expectedColumnCount = columnList.Count + (GetShowRowHeaderColumn(dataGrid) ? 1 : 0);
            if (dataGrid.Columns.Count == expectedColumnCount)
            {
                // 列数量匹配，假设列集合未变化，跳过更新以避免 UI 卡顿
                return;
            }
        }

        // 清空现有列
        dataGrid.Columns.Clear();

        var showRowHeader = GetShowRowHeaderColumn(dataGrid);
        var rowHeaderColumn = GetRowHeaderColumn(dataGrid);

        // 添加行标题列（如果启用）
        if (showRowHeader)
        {
            DataGridColumn rowColumn;
            if (rowHeaderColumn != null)
            {
                rowColumn = CreateColumn(rowHeaderColumn);
            }
            else
            {
                // 默认行标题列配置
                var defaultStyleKey = GetDefaultElementStyleKey(dataGrid);
                rowColumn = new DataGridTextColumn
                {
                    Header = "",
                    Width = DataGridLength.Auto,
                    IsReadOnly = true,
                    Binding = new Binding(nameof(DisplayRowItem.RowName)),
                };

                // 应用默认的 ElementStyle
                if (!string.IsNullOrEmpty(defaultStyleKey) && rowColumn is DataGridTextColumn textColumn)
                {
                    var defaultStyle = (Style?)Application.Current.TryFindResource(defaultStyleKey);
                    if (defaultStyle != null)
                    {
                        textColumn.ElementStyle = defaultStyle;
                    }
                }
            }
            dataGrid.Columns.Add(rowColumn);
        }

        // 添加数据列
        foreach (var col in columns)
        {
            var column = CreateColumn(col);
            dataGrid.Columns.Add(column);
        }

        // 标记为已初始化
        initializedDataGrids.Add(dataGrid, new object());
    }

    private static DataGridColumn CreateColumn(IDataGridColumnDescriptor descriptor)
    {
        DataGridColumn column = descriptor.ColumnType switch
        {
            DataGridColumnType.CheckBox => new DataGridCheckBoxColumn(),
            DataGridColumnType.ComboBox => new DataGridComboBoxColumn(),
            DataGridColumnType.Template => new DataGridTemplateColumn(),
            _ => new DataGridTextColumn()
        };

        // 设置通用属性
        column.Header = descriptor.Header;
        column.Width = descriptor.Width;
        column.IsReadOnly = descriptor.IsReadOnly;
        column.CanUserSort = descriptor.CanUserSort;
        column.CanUserResize = descriptor.CanUserResize;
        column.CanUserReorder = descriptor.CanUserReorder;

        // 设置绑定
        var binding = new Binding(descriptor.BindingPath)
        {
            Mode = descriptor.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay
        };

        if (descriptor.Converter != null)
        {
            binding.Converter = descriptor.Converter;
        }

        if (descriptor.ConverterParameter != null)
        {
            binding.ConverterParameter = descriptor.ConverterParameter;
        }

        if (!string.IsNullOrEmpty(descriptor.StringFormat))
        {
            binding.StringFormat = descriptor.StringFormat;
        }

        // 🌟 核心性能优化：动态创建一个密封的 CellStyle，绑定到 Judge
        if (descriptor is DataGridColumnDescriptor desc && !string.IsNullOrEmpty(desc.ParameterId))
        {
            // 如果你有全局的 DefaultCellStyle，可以把它传到 baseStyle 里
            var cellStyle = new Style(typeof(DataGridCell));

            // 背景色绑定路径：Item[key].Judge
            var bgBinding = new Binding($"Item[{desc.ParameterId}].Judge")
            {
                Converter = Kwy.UI.WPF.Converters.JudgeToBrushConverter.Instance
            };

            cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, bgBinding));

            // 务必 Seal() 冻结样式，这能让 WPF 渲染性能提升几十倍
            cellStyle.Seal();
            column.CellStyle = cellStyle;
        }

        // 根据列类型设置特定属性
        switch (column)
        {
            case DataGridTextColumn textColumn:
                textColumn.Binding = binding;

                // 设置 ElementStyle（优先级：ElementStyle > ElementStyleKey > HorizontalAlignment）
                if (descriptor.ElementStyle != null)
                {
                    textColumn.ElementStyle = descriptor.ElementStyle;
                }
                else if (!string.IsNullOrEmpty(descriptor.ElementStyleKey))
                {
                    var style = Application.Current.TryFindResource(descriptor.ElementStyleKey) as Style;
                    if (style != null)
                    {
                        textColumn.ElementStyle = style;
                    }
                }
                else if (descriptor.HorizontalAlignment.HasValue)
                {
                    // 如果没有指定样式，但有对齐方式，创建样式来设置对齐方式
                    var style = new Style(typeof(TextBlock));
                    style.Setters.Add(new Setter(
                        TextBlock.TextAlignmentProperty,
                        descriptor.HorizontalAlignment.Value));
                    textColumn.ElementStyle = style;
                }

                // 设置 EditingElementStyle
                if (descriptor.EditingElementStyle != null)
                {
                    textColumn.EditingElementStyle = descriptor.EditingElementStyle;
                }
                else if (!string.IsNullOrEmpty(descriptor.EditingElementStyleKey))
                {
                    var editingStyle = Application.Current.TryFindResource(descriptor.EditingElementStyleKey) as Style;
                    if (editingStyle != null)
                    {
                        textColumn.EditingElementStyle = editingStyle;
                    }
                }

                break;

            case DataGridCheckBoxColumn checkBoxColumn:
                checkBoxColumn.Binding = binding;
                break;

            case DataGridComboBoxColumn comboBoxColumn:
                comboBoxColumn.SelectedItemBinding = binding;
                // 注意：ComboBoxColumn 需要额外配置 ItemsSource
                break;

            case DataGridTemplateColumn templateColumn:
                // TemplateColumn 需要手动设置 CellTemplate 和 CellEditingTemplate
                // 这里简化处理，实际使用时应该通过扩展属性提供模板
                break;
        }

        return column;
    }
}

