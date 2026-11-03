namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    /// <summary>1,1 </summary>
    public static SecsMessageDefinition AreYouThereRequest { get; } =
        new(new SecsMessageId(1, 1), nameof(AreYouThereRequest), true);

    /// <summary>1,2 </summary>
    public static SecsMessageDefinition AreYouThereResponse { get; } =
        new(new SecsMessageId(1, 2), nameof(AreYouThereResponse), false);

    /// <summary>1,3 </summary>
    public static SecsMessageDefinition SelectedEquipmentStatusRequest { get; } =
        new(new SecsMessageId(1, 3), nameof(SelectedEquipmentStatusRequest), true);

    /// <summary>1,4 </summary>
    public static SecsMessageDefinition SelectedEquipmentStatusData { get; } =
        new(new SecsMessageId(1, 4), nameof(SelectedEquipmentStatusData), false);

    /// <summary>1,13 </summary>
    public static SecsMessageDefinition EstablishCommunicationsRequest { get; } =
        new(new SecsMessageId(1, 13), nameof(EstablishCommunicationsRequest), true);

    /// <summary>1,14 </summary>
    public static SecsMessageDefinition EstablishCommunicationsAcknowledge { get; } =
        new(new SecsMessageId(1, 14), nameof(EstablishCommunicationsAcknowledge), false);

    /// <summary>1,17 </summary>
    public static SecsMessageDefinition RequestOnline { get; } =
        new(new SecsMessageId(1, 17), nameof(RequestOnline), true);

    /// <summary>1,18 </summary>
    public static SecsMessageDefinition OnlineAcknowledge { get; } =
        new(new SecsMessageId(1, 18), nameof(OnlineAcknowledge), false);
}
