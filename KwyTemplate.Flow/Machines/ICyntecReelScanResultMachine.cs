namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 可选的 Cyntec Reel 扫码结果回执能力。
/// 通道是主 IO 卡的物理 DO 通道。
/// </summary>
public interface ICyntecReelScanResultMachine
{
    int ReelScanOkOutputChannel { get; }

    int ReelScanNgOutputChannel { get; }
}
