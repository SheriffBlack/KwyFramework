using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.App.Models;

/// <summary>
/// 从工单导入或在MES离线时本地编辑标记打印选项
/// </summary>
public sealed class MarkPrintOptions
{
    [DisplayName("编带字符")]
    [DisplayNameKey("MarkPrint.PrintString")]
    [InputType(InputType.TextBox)]
    public string? PrintString { get; set; }
}
