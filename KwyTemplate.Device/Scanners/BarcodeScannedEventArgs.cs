namespace KwyTemplate.Device.Scanners;

public sealed class BarcodeScannedEventArgs : EventArgs
{
    public BarcodeScannedEventArgs(string code, string rawText, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        RawText = rawText ?? string.Empty;
        Timestamp = timestamp;
    }

    public string Code { get; }

    public string RawText { get; }

    public DateTimeOffset Timestamp { get; }
}