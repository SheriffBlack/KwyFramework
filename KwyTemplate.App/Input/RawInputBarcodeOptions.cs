namespace KwyTemplate.App.Input;

public sealed class RawInputBarcodeOptions
{
    public bool EnableBackgroundInput { get; set; } = true;

    public TimeSpan KeystrokeTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 扫码完成后继续屏蔽扫码枪随后的普通键盘消息，避免末尾回车落到当前焦点控件。
    /// </summary>
    public TimeSpan PostScanKeyboardSuppression { get; set; } = TimeSpan.FromMilliseconds(500);

    public int MinBarcodeLength { get; set; } = 1;

    public bool TrimCode { get; set; } = true;
}
