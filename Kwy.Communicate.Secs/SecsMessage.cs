namespace Kwy.Communicate.Secs;

public sealed record SecsMessage(
    byte Stream,
    byte Function,
    bool ReplyExpected = false,
    SecsItem? Data = null,
    uint SystemBytes = 0,
    string? Name = null)
{
    public SecsMessage(
        SecsMessageCode messageCode,
        bool ReplyExpected = false,
        SecsItem? Data = null,
        uint SystemBytes = 0,
        string? Name = null)
        : this(
            messageCode.Stream,
            messageCode.Function,
            ReplyExpected,
            Data,
            SystemBytes,
            Name)
    {
    }

    public SecsMessageCode MessageCode => new(Stream, Function);

    public bool IsPrimary => MessageCode.IsPrimary;

    public string SxFy => MessageCode.ToString();

    public SecsMessage WithSystemBytes(uint systemBytes) => this with { SystemBytes = systemBytes };
}

public sealed record SecsMessageReceivedEventArgs(SecsMessage Message);
