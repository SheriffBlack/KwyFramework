using KwyPecvd.Process.Chamber.Gas;
using System.Collections.Immutable;

namespace KwyPecvd.Process.Chamber;

/// <summary>
/// 跨 RT 周期保存的当前腔体准备任务。
/// 仅在 Preparing 状态中存在。
/// </summary>
internal sealed record ChamberPreparationContext
{
    private static IReadOnlyDictionary<string, ChamberGasTarget> EmptyGasTargets { get; } = ImmutableDictionary.Create<string, ChamberGasTarget>(
            StringComparer.OrdinalIgnoreCase);

    public required ChamberPreparationRequirement Requirements { get; init; }

    public IReadOnlyDictionary<string, ChamberGasTarget> GasTargets { get; init; } = EmptyGasTargets;
}
