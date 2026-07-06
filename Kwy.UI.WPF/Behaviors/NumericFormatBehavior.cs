using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;
using System.Windows.Input;
using static Kwy.UI.WPF.Controls.Helpers.NumberFormatHelper;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// TextBox Text 数字格式化行为 1000000 -> 1,000,000 / 1_000_000
/// </summary>
public class NumericFormatBehavior : Behavior<TextBox>
{
    public INumberFormatService? FormatService { get; set; }

    protected override void OnAttached()
    {
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !char.IsDigit(e.Text, 0);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        if (tb == null || FormatService == null) return;

        // 避免递归调用
        if (e.Changes.Count == 0) return;

        var oldText = tb.Text; // 这里的 tb.Text 已经是改变后的了，等下，WPF TextChanged 触发时 Text 已经是新的了

        // 重新获取原始文本过程比较复杂，因为 Text 已经变了。
        // 但是我们的目标是：格式化后的 Text。
        // 逻辑：
        // 1. 获取当前光标位置
        // 2. 计算光标左侧有多少个“有效数字”
        // 3. 格式化文本
        // 4. 设置新文本
        // 5. 设置新光标位置：使得左侧有效数字个数与之前一致

        int caretIndex = tb.CaretIndex;

        // 获取未格式化的原始字符串
        var raw = FormatService.RemoveFormat(tb.Text);
        var formatted = FormatService.Format(raw);

        if (tb.Text == formatted)
            return;

        // 计算光标左侧的有效数字个数
        int effectiveDigitsBeforeCaret = 0;
        for (int i = 0; i < caretIndex && i < tb.Text.Length; i++)
        {
            if (char.IsDigit(tb.Text[i]) || tb.Text[i] == '.' || tb.Text[i] == '-') // 简单起见，认为这些不仅是分隔符
            {
                // 实际上只需要统计数字，因为分隔符是自动生成的
                if (char.IsDigit(tb.Text[i]))
                    effectiveDigitsBeforeCaret++;
            }
        }

        // 如果是在插入分隔符的位置输入，可能需要特殊处理，但通常统计数字就够了

        tb.Text = formatted;

        // 恢复光标位置
        int newCaretIndex = 0;
        int digitsSeen = 0;
        for (int i = 0; i < tb.Text.Length; i++)
        {
            if (char.IsDigit(tb.Text[i]))
            {
                digitsSeen++;
            }

            newCaretIndex = i + 1;

            if (digitsSeen == effectiveDigitsBeforeCaret)
            {
                // 如果当前已经是最后一位数字，或者下一个字符不是数字（是分隔符），我们需要跳过分隔符吗？
                // 通常习惯是光标停在数字后面。
                // 如果后面紧跟着分隔符，通常光标应该在分隔符前面还是后面？
                // 比如 1,|234 -> 输入 1 -> 1,1|234 (前面有2个数字)
                break;
            }
        }

        // 边界修正
        if (effectiveDigitsBeforeCaret == 0) newCaretIndex = 0;
        // 如果都在最后
        if (digitsSeen < effectiveDigitsBeforeCaret) newCaretIndex = tb.Text.Length;

        tb.CaretIndex = Math.Min(newCaretIndex, tb.Text.Length);
    }
}