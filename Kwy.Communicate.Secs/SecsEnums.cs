namespace Kwy.Communicate.Secs;

public enum HsmsConnectionMode
{
    Active,
    Passive
}

public enum HsmsSessionState
{
    NotConnected,
    Connected,
    Selected,
    NotSelected,
    Separating
}

public enum SecsItemFormat
{
    List,
    Binary,
    Boolean,
    Ascii,
    Jis8,
    Int1,
    Int2,
    Int4,
    Int8,
    UInt1,
    UInt2,
    UInt4,
    UInt8,
    Float4,
    Float8
}
