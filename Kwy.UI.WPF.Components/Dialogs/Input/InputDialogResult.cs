using System.Globalization;
using Kwy.MVVM.Dialogs;

namespace Kwy.UI.WPF.Components.Dialogs;

/// <summary>
/// 输入对话框结果。
/// </summary>
public sealed class InputDialogResult
{
    public InputDialogResult(ButtonResult result, string? value)
    {
        Result = result;
        Value = value ?? string.Empty;
    }

    /// <summary>
    /// 原始按钮结果。
    /// </summary>
    public ButtonResult Result { get; }

    /// <summary>
    /// 是否确认。
    /// </summary>
    public bool IsConfirmed => Result == ButtonResult.OK;

    /// <summary>
    /// 输入文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 尝试转换为 decimal。
    /// </summary>
    public bool TryGetDecimal(out decimal value)
        => decimal.TryParse(Value, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// 获取 decimal 值，转换失败时抛出异常。
    /// </summary>
    public decimal GetDecimal()
    {
        if (TryGetDecimal(out decimal value))
        {
            return value;
        }

        throw new FormatException($"输入值 '{Value}' 不是有效数值。");
    }

    /// <summary>
    /// 获取 int 值，转换失败时抛出异常。
    /// </summary>
    public int GetInt32() => decimal.ToInt32(GetDecimal());
}
