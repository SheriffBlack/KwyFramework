namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition EquipmentConstantSend { get; } =
        new(new SecsMessageId(2, 15), nameof(EquipmentConstantSend), true);
    public static SecsMessageDefinition EquipmentConstantAcknowledge { get; } =
        new(new SecsMessageId(2, 16), nameof(EquipmentConstantAcknowledge), false);
    public static SecsMessageDefinition TraceInitializeSend { get; } =
        new(new SecsMessageId(2, 23), nameof(TraceInitializeSend), true);
    public static SecsMessageDefinition TraceInitializeAcknowledge { get; } =
        new(new SecsMessageId(2, 24), nameof(TraceInitializeAcknowledge), false);
    public static SecsMessageDefinition HostCommandSend { get; } =
        new(new SecsMessageId(2, 41), nameof(HostCommandSend), true);
    public static SecsMessageDefinition HostCommandAcknowledge { get; } =
        new(new SecsMessageId(2, 42), nameof(HostCommandAcknowledge), false);
}
