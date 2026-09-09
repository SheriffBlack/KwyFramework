using Kwy.UI.WPF.Controls;

namespace Kwy.UI.WPF.Input;

public interface IKeyboardInputTarget
{
    KeyboardInputResult HandleKey(KeyboardKeyInvokedEventArgs input, KeyboardLayout layout);
}
