namespace KwyTemplate.App.Input;

public sealed class BarcodeInputReceivedEventArgs : EventArgs
{
    public BarcodeInputReceivedEventArgs(string code, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Timestamp = timestamp;
    }

    public string Code { get; }

    public DateTimeOffset Timestamp { get; }
}