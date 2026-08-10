namespace KwyTemplate.App.Input;

public interface IRawInputBarcodeReceiver : IDisposable
{
    event EventHandler<BarcodeInputReceivedEventArgs>? BarcodeReceived;

    bool IsInitialized { get; }

    string? LastCode { get; }

    void Initialize(IntPtr hwnd);
}