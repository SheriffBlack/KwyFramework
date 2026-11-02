namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition AreYouThereRequest { get; } =
        new(new SecsMessageId(1, 1), nameof(AreYouThereRequest), true);
    public static SecsMessageDefinition AreYouThereResponse { get; } =
        new(new SecsMessageId(1, 2), nameof(AreYouThereResponse), false);
    public static SecsMessageDefinition SelectedEquipmentStatusRequest { get; } =
        new(new SecsMessageId(1, 3), nameof(SelectedEquipmentStatusRequest), true);
    public static SecsMessageDefinition SelectedEquipmentStatusData { get; } =
        new(new SecsMessageId(1, 4), nameof(SelectedEquipmentStatusData), false);
    public static SecsMessageDefinition EstablishCommunicationsRequest { get; } =
        new(new SecsMessageId(1, 13), nameof(EstablishCommunicationsRequest), true);
    public static SecsMessageDefinition EstablishCommunicationsAcknowledge { get; } =
        new(new SecsMessageId(1, 14), nameof(EstablishCommunicationsAcknowledge), false);
    public static SecsMessageDefinition RequestOnline { get; } =
        new(new SecsMessageId(1, 17), nameof(RequestOnline), true);
    public static SecsMessageDefinition OnlineAcknowledge { get; } =
        new(new SecsMessageId(1, 18), nameof(OnlineAcknowledge), false);
}
