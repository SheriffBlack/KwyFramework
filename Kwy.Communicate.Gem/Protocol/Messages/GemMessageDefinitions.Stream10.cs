namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition TerminalRequest { get; } =
        new(new SecsMessageId(10, 1), nameof(TerminalRequest), true);
}
