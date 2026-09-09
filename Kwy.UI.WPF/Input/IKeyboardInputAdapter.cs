using Kwy.UI.WPF.Controls;

namespace Kwy.UI.WPF.Input;

public interface IKeyboardInputAdapter
{
    KeyboardInputResult HandleKey(KeyboardKeyInvokedEventArgs input, KeyboardLayout layout);
}
