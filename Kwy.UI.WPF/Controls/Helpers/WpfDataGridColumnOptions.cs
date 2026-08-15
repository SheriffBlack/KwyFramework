using Kwy.UI.DataGrids;
using Kwy.UI.Enums;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// WPF DataGrid 列描述。通用列字段来自 Kwy.UI.DataGrids，WPF 专属渲染选项留在 WPF 层。
/// </summary>
public class WpfDataGridColumnOptions : DataGridColumnDescriptor
{
    internal static WpfDataGridColumnOptions Default { get; } = new();

    public string? BindingPath { get; set; }

    public DataGridLength Width { get; set; } = new(1, DataGridLengthUnitType.Star);

    public DataGridColumnType ColumnType { get; set; } = DataGridColumnType.Text;

    public bool IsReadOnly { get; set; } = true;

    public TextAlignment? HorizontalAlignment { get; set; }

    public IValueConverter? Converter { get; set; }

    public object? ConverterParameter { get; set; }

    public string? StringFormat { get; set; }

    public bool CanUserSort { get; set; } = true;

    public bool CanUserResize { get; set; } = true;

    public bool CanUserReorder { get; set; } = true;

    public string? ElementStyleKey { get; set; } = "DataGridCellTextBlockStyle";

    public Style? ElementStyle { get; set; }

    public string? EditingElementStyleKey { get; set; }

    public Style? EditingElementStyle { get; set; }

    /// <summary>
    /// Template 列单元格模板的资源键。
    /// </summary>
    public string? CellTemplateKey { get; set; }
}
