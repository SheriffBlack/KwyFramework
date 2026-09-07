namespace Kwy.Communicate.Gem300;

public sealed record SlotMapItem(int Slot, bool HasSubstrate, string? SubstrateId = null);

public sealed record SlotMap(IReadOnlyList<SlotMapItem> Slots, SlotMapState State = SlotMapState.NotRead)
{
    public static SlotMap Empty(int slotCount)
        => new(Enumerable.Range(1, slotCount).Select(slot => new SlotMapItem(slot, false)).ToArray());
}

public sealed record Carrier(
    string CarrierId,
    int LoadPortId,
    SlotMap SlotMap,
    CarrierAccessState AccessState = CarrierAccessState.NotAccessed,
    CarrierTransferState TransferState = CarrierTransferState.Unknown,
    CarrierAssociationState AssociationState = CarrierAssociationState.NotAssociated);

public sealed record LoadPort(
    int LoadPortId,
    LoadPortState State,
    string? CarrierId = null);

public sealed record Substrate(
    string SubstrateId,
    string CarrierId,
    int Slot,
    SubstrateState State,
    string? Location = null,
    SubstrateLocationType LocationType = SubstrateLocationType.Unknown);

public sealed record ProcessJob(
    string ProcessJobId,
    string RecipeId,
    IReadOnlyList<string> SubstrateIds,
    JobState State = JobState.Created,
    ProcessJobType Type = ProcessJobType.Substrate,
    int Priority = 0);

public sealed record ControlJob(
    string ControlJobId,
    IReadOnlyList<string> ProcessJobIds,
    JobState State = JobState.Created,
    int Priority = 0);

public sealed record Gem300ObjectEvent(
    string ObjectType,
    string ObjectId,
    string EventName,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string>? Properties = null);
