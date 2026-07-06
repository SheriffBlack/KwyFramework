using System.ComponentModel;

namespace Kwy.Communicate.Abstractions.Enums;

public enum StopBitsType
{
    [Description("None")]
    None = 0x00,

    [Description("One")]
    One = 0x01,

    [Description("Two")]
    Two = 0x02,

    [Description("OnePointFive")]
    OnePointFive = 0x03,
}
