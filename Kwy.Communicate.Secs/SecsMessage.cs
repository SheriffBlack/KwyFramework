namespace Kwy.Communicate.Secs;

public sealed record SecsMessage(
    byte Stream,
    byte Function,
    bool ReplyExpected = false,
    SecsItem? Data = null,
    uint SystemBytes = 0,
    string? Name = null)
{
    public bool IsPrimary => Function % 2 == 1;

    public string SxFy => $"S{Stream}F{Function}";

    public SecsMessage WithSystemBytes(uint systemBytes) => this with { SystemBytes = systemBytes };
}

public sealed record SecsMessageReceivedEventArgs(SecsMessage Message);
