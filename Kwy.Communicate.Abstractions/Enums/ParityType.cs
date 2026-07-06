using System.ComponentModel;

namespace Kwy.Communicate.Abstractions.Enums;

public enum ParityType
{
    [Description("None")]
    None = 0x00,

    [Description("Odd")]
    Odd = 0x01,

    [Description("Even")]
    Even = 0x02,

    [Description("Mark")]
    Mark = 0x03,

    [Description("Space")]
    Space = 0x04,
}
