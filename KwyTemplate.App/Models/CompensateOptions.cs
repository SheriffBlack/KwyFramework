using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.App.Models;

/// <summary>
/// 自动点检配置。通过 UI 元数据驱动 KwyPropertyGrid 生成设置界面。
/// </summary>
public sealed class CompensateOptions
{
    [DisplayName("点检开关")]
    [DisplayNameKey("CompensateOptions.IsEnabled")]
    [InputType(InputType.ToggleButton)]
    [GroupWidth(0.5)]
    [InlineGroup("AutoCheck")]
    public bool IsEnabled { get; set; } = true;

    [DisplayName("点检窗口（小时）")]
    [DisplayNameKey("CompensateOptions.CheckWindow")]
    [InputType(InputType.TextBox)]
    [GroupWidth(0.5)]
    [InlineGroup("AutoCheck")]
    public double CheckWindow { get; set; } = 2.0;

    [Category("A班")]
    [CategoryKey("CompensateOptions.Category.ShiftA")]
    [DisplayName("时间1（时）")]
    [DisplayNameKey("CompensateOptions.Time1")]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [EditorWidth(120)]
    [ItemsSource("8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19")]
    public string CompensateATime1 { get; set; } = "8";

    [Category("A班")]
    [CategoryKey("CompensateOptions.Category.ShiftA")]
    [DisplayName("时间2（时）")]
    [DisplayNameKey("CompensateOptions.Time2")]
    [InputType(InputType.ComboBox)]
    [EditorWidth(120)]
    [ItemsSource("8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19")]
    public string CompensateATime2 { get; set; } = "14";

    [Category("B班")]
    [CategoryKey("CompensateOptions.Category.ShiftB")]
    [DisplayName("时间1（时）")]
    [DisplayNameKey("CompensateOptions.Time1")]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [EditorWidth(120)]
    [ItemsSource("20", "21", "22", "23", "00", "1", "2", "3", "4", "5", "6", "7")]
    public string CompensateBTime1 { get; set; } = "20";

    [Category("B班")]
    [CategoryKey("CompensateOptions.Category.ShiftB")]
    [DisplayName("时间2（时）")]
    [DisplayNameKey("CompensateOptions.Time2")]
    [InputType(InputType.ComboBox)]
    [EditorWidth(120)]
    [ItemsSource("20", "21", "22", "23", "00", "1", "2", "3", "4", "5", "6", "7")]
    public string CompensateBTime2 { get; set; } = "2";
}