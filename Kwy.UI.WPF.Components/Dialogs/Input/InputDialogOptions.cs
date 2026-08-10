namespace Kwy.UI.WPF.Components.Dialogs;

/// <summary>
/// 输入对话框参数。
/// </summary>
public sealed class InputDialogOptions
{
    /// <summary>
    /// 对话框标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 提示文本。
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 输入项标签。
    /// </summary>
    public string Label { get; set; } = "输入";

    /// <summary>
    /// 默认输入值。
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 输入类型。
    /// </summary>
    public InputDialogType InputType { get; set; } = InputDialogType.Text;

    /// <summary>
    /// 数值输入最小值。
    /// </summary>
    public decimal? Minimum { get; set; }

    /// <summary>
    /// 数值输入最大值。
    /// </summary>
    public decimal? Maximum { get; set; }

    /// <summary>
    /// 数值单位。
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// 确认按钮文本。
    /// </summary>
    public string ConfirmButtonText { get; set; } = "确定";

    /// <summary>
    /// 取消按钮文本。
    /// </summary>
    public string CancelButtonText { get; set; } = "取消";

    /// <summary>
    /// 是否显示取消按钮。
    /// </summary>
    public bool ShowCancelButton { get; set; } = true;
}
