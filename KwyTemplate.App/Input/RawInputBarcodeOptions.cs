namespace KwyTemplate.App.Input;

public sealed class RawInputBarcodeOptions
{
    public bool EnableBackgroundInput { get; set; } = true;

    public TimeSpan KeystrokeTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    public int MinBarcodeLength { get; set; } = 1;

    public bool TrimCode { get; set; } = true;
}