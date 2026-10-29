namespace KwyPecvd.Process.Chamber;

/// <summary>本轮腔体准备必须满足的子系统条件。</summary>
[Flags]
internal enum ChamberPreparationRequirement
{
    None = 0,
    Gas = 1 << 0,
    Vacuum = 1 << 1
}
