namespace KwyPecvd.Process.Chambers;

/// <summary>
/// 为指定腔体提供最新联锁状态。
/// 提供事实：门关没关、真空好不好
/// </summary>
public interface IChamberInterlockSource
{
    ChamberInterlockSnapshot Snapshot { get; }
}
