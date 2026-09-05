using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.App.Models;

/// <summary>
/// Property-grid metadata for the non-instrument part of an offline work-order.
/// It deliberately uses internal recipe semantics rather than customer MES keys.
/// </summary>
public sealed class LocalWorkOrderFunctionOptions
{
    [Category("物料要求")]
    [DisplayName("台纸料号")]
    [DisplayNameKey("WorkOrderOptions.TablePaperMatNo")]
    [InputType(InputType.TextBox)]
    public string? TablePaperMatNo { get; set; }

    [Category("物料要求")]
    [DisplayName("上盖料号")]
    [DisplayNameKey("WorkOrderOptions.TopCoverMatNo")]
    [InputType(InputType.TextBox)]
    public string? TopCoverMatNo { get; set; }

    [Category("配方信息")]
    [DisplayName("料组号")]
    [DisplayNameKey("WorkOrderOptions.RecipeName")]
    [InputType(InputType.TextBox)]
    public string? RecipeName { get; set; }
}
