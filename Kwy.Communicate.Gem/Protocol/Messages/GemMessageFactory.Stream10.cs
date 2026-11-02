using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    public static SecsMessage TerminalMessage(GemTerminalMessage message)
        => Create(GemMessageDefinitions.TerminalRequest, L(B(message.TerminalId), A(message.Text)));
}
