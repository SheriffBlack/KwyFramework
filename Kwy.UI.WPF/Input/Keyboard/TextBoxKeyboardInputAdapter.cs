using Kwy.UI.WPF.Controls;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;

namespace Kwy.UI.WPF.Input.Keyboard;

public sealed class TextBoxKeyboardInputAdapter : IKeyboardInputAdapter
{
    private readonly TextBox textBox;
    private readonly NumericKeyboardOptions? numericOptions;

    public TextBoxKeyboardInputAdapter(TextBox textBox, NumericKeyboardOptions? numericOptions = null)
    {
        this.textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
        this.numericOptions = numericOptions;
    }

    public KeyboardInputResult HandleKey(KeyboardKeyInvokedEventArgs input, KeyboardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (textBox.IsReadOnly || !textBox.IsEnabled)
        {
            return KeyboardInputResult.Handled;
        }

        if (input.Control && HandleControlShortcut(input.Key))
        {
            return KeyboardInputResult.Handled;
        }

        switch (input.Key)
        {
            case Key.Clear:
                textBox.Clear();
                return KeyboardInputResult.Handled;
            case Key.Add when numericOptions != null:
                SetSign(false);
                return KeyboardInputResult.Handled;
            case Key.Subtract when numericOptions != null:
                if (numericOptions.AllowNegative) SetSign(true);
                return KeyboardInputResult.Handled;
            case Key.Back:
                Backspace();
                return KeyboardInputResult.Handled;
            case Key.Delete:
                Delete();
                return KeyboardInputResult.Handled;
            case Key.Left:
                MoveCaret(-1, input.Shift);
                return KeyboardInputResult.Handled;
            case Key.Right:
                MoveCaret(1, input.Shift);
                return KeyboardInputResult.Handled;
            case Key.Home:
                SetCaret(0, input.Shift);
                return KeyboardInputResult.Handled;
            case Key.End:
                SetCaret(textBox.Text.Length, input.Shift);
                return KeyboardInputResult.Handled;
            case Key.Enter:
                if (textBox.AcceptsReturn)
                {
                    InsertText(Environment.NewLine);
                    return KeyboardInputResult.Handled;
                }
                return IsValidCommittedNumber() ? KeyboardInputResult.Commit : KeyboardInputResult.Handled;
            case Key.Escape:
                return KeyboardInputResult.Cancel;
            case Key.Tab:
                return KeyboardInputResult.Commit;
        }

        string? text = numericOptions != null && input.Key == Key.Decimal
            ? CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
            : ResolveText(input.Key, input.Shift, input.CapsLock, layout);
        if (text != null)
        {
            TryInsertText(text);
        }

        return KeyboardInputResult.Handled;
    }

    private bool HandleControlShortcut(Key key)
    {
        switch (key)
        {
            case Key.A:
                textBox.SelectAll();
                return true;
            case Key.C:
                textBox.Copy();
                return true;
            case Key.X:
                textBox.Cut();
                return true;
            case Key.V:
                textBox.Paste();
                return true;
            default:
                return false;
        }
    }

    private void InsertText(string value)
    {
        int start = textBox.SelectionStart;
        textBox.SelectedText = value;
        textBox.CaretIndex = start + value.Length;
        textBox.SelectionLength = 0;
    }

    private void TryInsertText(string value)
    {
        if (numericOptions == null)
        {
            InsertText(value);
            return;
        }

        int start = textBox.SelectionStart;
        string candidate = textBox.Text.Remove(start, textBox.SelectionLength).Insert(start, value);
        if (IsValidEditingNumber(candidate))
        {
            InsertText(value);
        }
    }

    private void SetSign(bool negative)
    {
        string unsigned = textBox.Text.TrimStart('+', '-');
        string candidate = negative ? $"-{unsigned}" : $"+{unsigned}";
        if (!IsValidEditingNumber(candidate))
        {
            return;
        }

        textBox.Text = candidate;
        textBox.CaretIndex = textBox.Text.Length;
        textBox.SelectionLength = 0;
    }

    private bool IsValidEditingNumber(string candidate)
    {
        if (numericOptions == null) return true;
        string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (candidate.Length == 0 || candidate == "+" || (numericOptions.AllowNegative && candidate == "-")
            || (numericOptions.AllowDecimal
                && (candidate == separator || candidate == $"+{separator}" || candidate == $"-{separator}")))
        {
            return true;
        }

        if (!numericOptions.AllowNegative && candidate.StartsWith("-", StringComparison.Ordinal)) return false;
        if (!numericOptions.AllowDecimal && candidate.Contains(separator, StringComparison.Ordinal)) return false;
        int separatorIndex = candidate.IndexOf(separator, StringComparison.Ordinal);
        if (numericOptions.DecimalPlaces is int places && separatorIndex >= 0
            && candidate.Length - separatorIndex - separator.Length > places) return false;

        return double.TryParse(candidate, NumberStyles.Float, CultureInfo.CurrentCulture, out _);
    }

    private bool IsValidCommittedNumber()
    {
        if (numericOptions == null) return true;
        if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)) return false;
        return (numericOptions.Minimum is not double minimum || value >= minimum)
            && (numericOptions.Maximum is not double maximum || value <= maximum);
    }

    private void Backspace()
    {
        if (textBox.SelectionLength > 0)
        {
            InsertText(string.Empty);
            return;
        }

        if (textBox.CaretIndex > 0)
        {
            int index = textBox.CaretIndex;
            textBox.Select(index - 1, 1);
            InsertText(string.Empty);
        }
    }

    private void Delete()
    {
        if (textBox.SelectionLength > 0)
        {
            InsertText(string.Empty);
            return;
        }

        if (textBox.CaretIndex < textBox.Text.Length)
        {
            textBox.Select(textBox.CaretIndex, 1);
            InsertText(string.Empty);
        }
    }

    private void MoveCaret(int offset, bool extendSelection)
        => SetCaret(Math.Clamp(textBox.CaretIndex + offset, 0, textBox.Text.Length), extendSelection);

    private void SetCaret(int index, bool extendSelection)
    {
        index = Math.Clamp(index, 0, textBox.Text.Length);
        if (!extendSelection)
        {
            textBox.CaretIndex = index;
            textBox.SelectionLength = 0;
            return;
        }

        int anchor = textBox.SelectionLength == 0
            ? textBox.CaretIndex
            : textBox.SelectionStart;
        textBox.Select(Math.Min(anchor, index), Math.Abs(index - anchor));
        textBox.CaretIndex = index;
    }

    private static string? ResolveText(Key key, bool shift, bool capsLock, KeyboardLayout layout)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            char letter = (char)('a' + ((int)key - (int)Key.A));
            letter = layout switch
            {
                KeyboardLayout.Azerty when letter == 'a' => 'q',
                KeyboardLayout.Azerty when letter == 'q' => 'a',
                KeyboardLayout.Azerty when letter == 'w' => 'z',
                KeyboardLayout.Azerty when letter == 'z' => 'w',
                KeyboardLayout.Qwertz when letter == 'y' => 'z',
                KeyboardLayout.Qwertz when letter == 'z' => 'y',
                _ => letter
            };
            return (shift ^ capsLock ? char.ToUpperInvariant(letter) : letter).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            const string shifted = ")!@#$%^&*(";
            int index = (int)key - (int)Key.D0;
            return shift ? shifted[index].ToString() : index.ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)key - (int)Key.NumPad0).ToString();
        }

        return key switch
        {
            Key.Space => " ",
            Key.Decimal or Key.OemPeriod => shift ? ">" : ".",
            Key.OemComma => shift ? "<" : ",",
            Key.Add or Key.OemPlus => shift && key == Key.OemPlus ? "+" : key == Key.Add ? "+" : "=",
            Key.Subtract or Key.OemMinus => shift && key == Key.OemMinus ? "_" : "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            Key.OemQuestion => shift ? "?" : "/",
            Key.OemSemicolon => shift ? ":" : ";",
            Key.OemQuotes => shift ? "\"" : "'",
            Key.OemOpenBrackets => shift ? "{" : "[",
            Key.OemCloseBrackets => shift ? "}" : "]",
            Key.OemPipe => shift ? "|" : "\\",
            Key.OemTilde => shift ? "~" : "`",
            _ => null
        };
    }
}
