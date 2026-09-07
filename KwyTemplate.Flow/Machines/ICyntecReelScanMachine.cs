namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 支持 Cyntec Reel 扫码流程的机型能力，暴露触发扫码枪采集的 DI 通道。
/// </summary>
public interface ICyntecReelScanMachine
{
    int ReelScanInputChannel { get; }
}