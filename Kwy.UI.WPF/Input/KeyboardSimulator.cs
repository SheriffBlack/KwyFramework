using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Kwy.UI.WPF.Input;

/// <summary>
/// Sends keyboard input through the Windows input subsystem.
/// </summary>
public static class KeyboardSimulator
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public static bool IsCapsLockEnabled => Keyboard.IsKeyToggled(Key.CapsLock);

    public static void SendKey(Key key)
    {
        Send(key, false);
        Send(key, true);
    }

    public static void SendChord(IEnumerable<Key> modifiers, Key key)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        Key[] modifierKeys = modifiers.ToArray();
        foreach (Key modifier in modifierKeys)
        {
            Send(modifier, false);
        }

        SendKey(key);

        for (int i = modifierKeys.Length - 1; i >= 0; i--)
        {
            Send(modifierKeys[i], true);
        }
    }

    private static void Send(Key key, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = checked((ushort)KeyInterop.VirtualKeyFromKey(key)),
                    Flags = keyUp ? KeyEventKeyUp : 0
                }
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
