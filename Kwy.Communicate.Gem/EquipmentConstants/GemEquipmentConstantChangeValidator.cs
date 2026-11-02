using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed record GemEquipmentConstantChange(GemEcid Ecid, Item Value);

/// <summary>
/// 根据 GEM 接口目录校验 Host 通过 S2F15 提交的设备常量变更请求。
/// </summary>
/// <remarks>
/// 本校验器只检查接口合同，不执行范围判定、设备安全互锁、持久化或 PLC 写入。
/// </remarks>
public sealed class GemEquipmentConstantChangeValidator : IGemValidator<IReadOnlyList<GemEquipmentConstantChange>>
{
    private readonly IGemInterfaceCatalog catalog;

    public GemEquipmentConstantChangeValidator(IGemInterfaceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
    }

    public GemValidationResult Validate(IReadOnlyList<GemEquipmentConstantChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
            return GemValidationResult.Failure("At least one equipment constant is required.");

        IGrouping<uint, GemEquipmentConstantChange>? duplicate = changes
            .GroupBy(change => change.Ecid.Value)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            return GemValidationResult.Failure($"ECID {duplicate.Key} occurs more than once in the request.");

        foreach (GemEquipmentConstantChange change in changes)
        {
            if (!catalog.Constants.TryGetValue(change.Ecid.Value, out GemEquipmentConstantDefinition? definition))
                return GemValidationResult.Failure($"ECID {change.Ecid.Value} is not defined.");
            if (definition.Format is SecsFormat expected && change.Value.Format != expected)
                return GemValidationResult.Failure(
                    $"ECID {change.Ecid.Value} expects {expected}, but received {change.Value.Format}.");
        }
        return GemValidationResult.Success;
    }
}
