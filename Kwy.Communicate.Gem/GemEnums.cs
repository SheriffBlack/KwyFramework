namespace Kwy.Communicate.Gem;

public enum GemCommunicationState
{
    Disabled,
    Enabled,
    NotCommunicating,
    Communicating
}

public enum GemControlState
{
    Offline,
    AttemptOnline,
    HostOffline,
    OnlineLocal,
    OnlineRemote
}

public enum GemAlarmState
{
    Set,
    Clear
}

public enum GemAckCode : byte
{
    Accepted = 0,
    Denied = 1,
    InvalidState = 2,
    InvalidParameter = 3,
    Busy = 4
}

public enum GemHostRole
{
    Equipment,
    Host
}

public enum GemVariableKind
{
    DataVariable,
    StatusVariable,
    EquipmentConstant
}

public enum GemRecipeState
{
    Created,
    Validated,
    Selected,
    Active,
    Archived,
    Rejected
}

public enum GemSpoolingState
{
    Disabled,
    Enabled,
    Active,
    Transmitting,
    Purging
}

public enum GemTraceState
{
    Disabled,
    Enabled,
    Active,
    Completed,
    Cancelled
}
