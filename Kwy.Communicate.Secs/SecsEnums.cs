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

/// <summary>
/// HSMS message type stored in the SType header field.
/// </summary>
public enum HsmsMessageType : byte
{
    DataMessage = 0,
    SelectRequest = 1,
    SelectResponse = 2,
    DeselectRequest = 3,
    DeselectResponse = 4,
    LinktestRequest = 5,
    LinktestResponse = 6,
    RejectRequest = 7,
    SeparateRequest = 9
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
