using System.Windows;
using System.Windows.Input;

namespace Kwy.UI.WPF.Controls;

public sealed class KeyboardKeyInvokedEventArgs : RoutedEventArgs
{
    public KeyboardKeyInvokedEventArgs(RoutedEvent routedEvent, Key key, bool shift, bool control, bool alt, bool capsLock)
        : base(routedEvent)
    {
        Key = key;
        Shift = shift;
        Control = control;
        Alt = alt;
        CapsLock = capsLock;
    }

    public Key Key { get; }
    public bool Shift { get; }
    public bool Control { get; }
    public bool Alt { get; }
    public bool CapsLock { get; }
}
