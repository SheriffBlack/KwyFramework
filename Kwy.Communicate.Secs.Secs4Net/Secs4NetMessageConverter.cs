using KwyMessage = Kwy.Communicate.Secs.SecsMessage;
using Secs4NetMessage = global::Secs4Net.SecsMessage;

namespace Kwy.Communicate.Secs.Secs4Net;

internal static class Secs4NetMessageConverter
{
    public static Secs4NetMessage ToSecs4Net(KwyMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new Secs4NetMessage(message.Stream, message.Function, message.ReplyExpected)
        {
            Name = message.Name,
            SecsItem = Secs4NetItemConverter.ToSecs4Net(message.Data)
        };
    }

    public static KwyMessage FromSecs4Net(Secs4NetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new KwyMessage(
            message.S,
            message.F,
            message.ReplyExpected,
            Secs4NetItemConverter.FromSecs4Net(message.SecsItem),
            Name: message.Name);
    }
}
