using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace KwyTemplate.App.Input;

public sealed class RawInputBarcodeReceiver : IRawInputBarcodeReceiver
{
    private const int WmInput = 0x00FF;
    private const int RidInput = 0x10000003;
    private const int RimTypeKeyboard = 1;
    private const int RidevInputSink = 0x00000100;
    private const int RiKeyBreak = 0x01;
    private const ushort VkEnter = 13;
    private const ushort VkTab = 9;
    private const ushort VkShift = 0x10;
    private const ushort VkLeftShift = 0xA0;
    private const ushort VkRightShift = 0xA1;

    private readonly RawInputBarcodeOptions options;
    private readonly StringBuilder barcodeBuilder = new();
    private HwndSource? hwndSource;
    private DateTime lastKeystroke = DateTime.Now;
    private bool isShiftPressed;
    private bool disposed;

    public RawInputBarcodeReceiver(RawInputBarcodeOptions? options = null)
    {
        this.options = options ?? new RawInputBarcodeOptions();
    }

    public event EventHandler<BarcodeInputReceivedEventArgs>? BarcodeReceived;

    public bool IsInitialized { get; private set; }

    public string? LastCode { get; private set; }

    public void Initialize(IntPtr hwnd)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));
        }

        DisposeHook();

        hwndSource = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("Cannot resolve HwndSource from window handle.");
        hwndSource.AddHook(WndProc);

        var rawInputDevices = new[]
        {
            new RawInputDevice
            {
                UsagePage = 0x01,
                Usage = 0x06,
                Flags = options.EnableBackgroundInput ? RidevInputSink : 0,
                Target = hwnd
            }
        };

        if (!RegisterRawInputDevices(rawInputDevices, (uint)rawInputDevices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            DisposeHook();
            throw new InvalidOperationException($"RegisterRawInputDevices failed. Win32Error={Marshal.GetLastWin32Error()}.");
        }

        IsInitialized = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmInput)
        {
            ProcessRawInput(lParam);
        }

        return IntPtr.Zero;
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        uint size = 0;
        _ = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        if (size == 0)
        {
            return;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(rawInputHandle, RidInput, buffer, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) != size)
            {
                return;
            }

            RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeKeyboard)
            {
                return;
            }

            IntPtr keyboardPointer = IntPtr.Add(buffer, Marshal.SizeOf<RawInputHeader>());
            RawKeyboard keyboard = Marshal.PtrToStructure<RawKeyboard>(keyboardPointer);
            bool isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
            ProcessKey(keyboard.VirtualKey, isKeyDown);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void ProcessKey(ushort virtualKey, bool isKeyDown)
    {
        if (virtualKey is VkShift or VkLeftShift or VkRightShift)
        {
            isShiftPressed = isKeyDown;
            return;
        }

        if (!isKeyDown)
        {
            return;
        }

        TimeSpan elapsed = DateTime.Now - lastKeystroke;
        if (elapsed > options.KeystrokeTimeout)
        {
            barcodeBuilder.Clear();
        }

        if (virtualKey == VkEnter)
        {
            PublishBarcode();
        }
        else
        {
            char? character = MapVirtualKeyToChar(virtualKey, isShiftPressed);
            if (character.HasValue)
            {
                barcodeBuilder.Append(character.Value);
            }
        }

        lastKeystroke = DateTime.Now;
    }

    private void PublishBarcode()
    {
        if (barcodeBuilder.Length == 0)
        {
            return;
        }

        string code = barcodeBuilder.ToString();
        barcodeBuilder.Clear();

        if (options.TrimCode)
        {
            code = code.Trim();
        }

        if (code.Length < Math.Max(1, options.MinBarcodeLength))
        {
            return;
        }

        LastCode = code;
        BarcodeReceived?.Invoke(this, new BarcodeInputReceivedEventArgs(code, DateTimeOffset.Now));
    }

    private static char? MapVirtualKeyToChar(ushort virtualKey, bool isShift)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return isShift ? (char)virtualKey : (char)(virtualKey + 32);
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            return (char)(virtualKey - 0x60 + '0');
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            if (!isShift)
            {
                return (char)virtualKey;
            }

            return virtualKey switch
            {
                0x30 => ')',
                0x31 => '!',
                0x32 => '@',
                0x33 => '#',
                0x34 => '$',
                0x35 => '%',
                0x36 => '^',
                0x37 => '&',
                0x38 => '*',
                0x39 => '(',
                _ => null
            };
        }

        return virtualKey switch
        {
            0x20 => ' ',
            0xBD => isShift ? '_' : '-',
            0xBB => isShift ? '+' : '=',
            0xDB => isShift ? '{' : '[',
            0xDD => isShift ? '}' : ']',
            0xDC => isShift ? '|' : '\\',
            0xBA => isShift ? ':' : ';',
            0xDE => isShift ? '"' : '\'',
            0xBC => isShift ? '<' : ',',
            0xBE => isShift ? '>' : '.',
            0xBF => isShift ? '?' : '/',
            0xC0 => isShift ? '~' : '`',
            0x6A => '*',
            0x6B => '+',
            0x6D => '-',
            0x6E => '.',
            0x6F => '/',
            _ => null
        };
    }

    private void DisposeHook()
    {
        if (hwndSource is not null)
        {
            hwndSource.RemoveHook(WndProc);
            hwndSource = null;
        }

        IsInitialized = false;
        barcodeBuilder.Clear();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeHook();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] rawInputDevices,
        uint numberDevices,
        uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public int Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }
}