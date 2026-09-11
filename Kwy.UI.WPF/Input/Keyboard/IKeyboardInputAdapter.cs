namespace Kwy.UI.WPF.Input.Keyboard;

public interface IKeyboardInputAdapter
{
    KeyboardInputResult HandleKey(KeyboardKeyInvokedEventArgs input, KeyboardLayout layout);
}
