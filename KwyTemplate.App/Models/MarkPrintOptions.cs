using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.ComponentModel.Attributes;

namespace KwyTemplate.App.Models;

/// <summary>
/// 从工单导入或在MES离线时本地编辑标记打印选项
/// </summary>
public sealed class MarkPrintOptions
{
    private string? printString;

    [DisplayName("编带字符")]
    [DisplayNameKey("MarkPrint.PrintString")]
    [InputType(InputType.TextBox)]
    public string? PrintString
    {
        get => printString;
        set => printString = value is null
            ? null
            : string.Join(' ', value.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
