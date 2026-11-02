using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>构造标准 SxFy + Data。</summary>
public static partial class GemMessageFactory
{
    private static SecsMessage Create(SecsMessageDefinition definition, Item? item = null)
        => new(definition.Id.Stream, definition.Id.Function, definition.ReplyExpected)
        {
            Name = definition.Name,
            SecsItem = item
        };
}
