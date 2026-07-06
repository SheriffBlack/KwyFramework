using System.ComponentModel;

namespace Kwy.Communicate.Abstractions.Enums;

public enum HandshakeType
{
    [Description("None")]
    None = 0x00,

    [Description("XOnXOff")]
    XOnXOff = 0x01,

    [Description("RequestToSend")]
    RequestToSend = 0x02,

    [Description("RequestToSendXOnXOff")]
    RequestToSendXOnXOff = 0x03,
}
