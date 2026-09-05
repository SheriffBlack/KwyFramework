namespace KwyTemplate.App.Input;

public interface IRawInputBarcodeReceiver : IDisposable
{
    /// <summary>
    /// 当前是否正在接收一段尚未以回车结束的扫码输入。
    /// 供宿主窗口屏蔽扫码枪意外附带的系统快捷键。
    /// </summary>
    bool IsScanInProgress { get; }

    /// <summary>
    /// 当前普通键盘消息是否应被宿主窗口屏蔽。
    /// 包含扫码进行中及扫码刚结束时的尾部按键。
    /// </summary>
    bool ShouldSuppressKeyboardInput { get; }

    event EventHandler<BarcodeInputReceivedEventArgs>? BarcodeReceived;

    /// <summary>原始扫码按键诊断事件，仅用于定位扫码期间的窗口状态异常。</summary>
    event EventHandler<RawInputBarcodeDiagnosticEventArgs>? DiagnosticOccurred;

    bool IsInitialized { get; }

    string? LastCode { get; }

    void Initialize(IntPtr hwnd);
}

public enum RawInputBarcodeDiagnosticKind
{
    ScanStarted,
    KeyReceived,
    ScanCompleted
}

public sealed class RawInputBarcodeDiagnosticEventArgs : EventArgs
{
    public RawInputBarcodeDiagnosticEventArgs(
        RawInputBarcodeDiagnosticKind kind,
        ushort? virtualKey = null,
        bool isShiftPressed = false,
        int bufferLength = 0,
        string? code = null)
    {
        Kind = kind;
        VirtualKey = virtualKey;
        IsShiftPressed = isShiftPressed;
        BufferLength = bufferLength;
        Code = code;
    }

    public RawInputBarcodeDiagnosticKind Kind { get; }
    public ushort? VirtualKey { get; }
    public bool IsShiftPressed { get; }
    public int BufferLength { get; }
    public string? Code { get; }
}
