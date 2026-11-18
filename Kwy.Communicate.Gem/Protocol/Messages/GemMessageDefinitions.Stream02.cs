namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    /// <summary>2,15 </summary>
    public static SecsMessageDefinition EquipmentConstantSend { get; } =
        new(new SecsMessageId(2, 15), nameof(EquipmentConstantSend), true);

    /// <summary>2,16 </summary>
    public static SecsMessageDefinition EquipmentConstantAcknowledge { get; } =
        new(new SecsMessageId(2, 16), nameof(EquipmentConstantAcknowledge), false);

    /// <summary>2,23 </summary>
    public static SecsMessageDefinition TraceInitializeSend { get; } =
        new(new SecsMessageId(2, 23), nameof(TraceInitializeSend), true);

    /// <summary>2,24 </summary>
    public static SecsMessageDefinition TraceInitializeAcknowledge { get; } =
        new(new SecsMessageId(2, 24), nameof(TraceInitializeAcknowledge), false);

    /// <summary>2,41 </summary>
    public static SecsMessageDefinition HostCommandSend { get; } =
        new(new SecsMessageId(2, 41), nameof(HostCommandSend), true);

    /// <summary>2,42 </summary>
    public static SecsMessageDefinition HostCommandAcknowledge { get; } =
        new(new SecsMessageId(2, 42), nameof(HostCommandAcknowledge), false);
}
