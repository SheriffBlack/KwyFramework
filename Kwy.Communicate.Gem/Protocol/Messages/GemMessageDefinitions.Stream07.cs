namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition ProcessProgramLoadInquire { get; } =
        new(new SecsMessageId(7, 1), nameof(ProcessProgramLoadInquire), true);
    public static SecsMessageDefinition ProcessProgramLoadGrant { get; } =
        new(new SecsMessageId(7, 2), nameof(ProcessProgramLoadGrant), false);
    public static SecsMessageDefinition ProcessProgramSend { get; } =
        new(new SecsMessageId(7, 3), nameof(ProcessProgramSend), true);
    public static SecsMessageDefinition ProcessProgramAcknowledge { get; } =
        new(new SecsMessageId(7, 4), nameof(ProcessProgramAcknowledge), false);
    public static SecsMessageDefinition ProcessProgramRequest { get; } =
        new(new SecsMessageId(7, 5), nameof(ProcessProgramRequest), true);
    public static SecsMessageDefinition ProcessProgramData { get; } =
        new(new SecsMessageId(7, 6), nameof(ProcessProgramData), false);
    public static SecsMessageDefinition DeleteProcessProgram { get; } =
        new(new SecsMessageId(7, 17), nameof(DeleteProcessProgram), true);
    public static SecsMessageDefinition DeleteProcessProgramAcknowledge { get; } =
        new(new SecsMessageId(7, 18), nameof(DeleteProcessProgramAcknowledge), false);
    public static SecsMessageDefinition CurrentProcessProgramDirectoryRequest { get; } =
        new(new SecsMessageId(7, 19), nameof(CurrentProcessProgramDirectoryRequest), true);
    public static SecsMessageDefinition CurrentProcessProgramDirectoryData { get; } =
        new(new SecsMessageId(7, 20), nameof(CurrentProcessProgramDirectoryData), false);
}
