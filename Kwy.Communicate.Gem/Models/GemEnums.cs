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

/// <summary>
/// 表示远程命令在设备业务中的最终执行状态。
/// </summary>
public enum GemRemoteCommandCompletionStatus
{
    Unspecified,
    Rejected,
    Completed,
    Failed,
    Cancelled
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

public enum GemProcessProgramState
{
    Created,
    Validated,
    Selected,
    Active,
    Archived,
    Rejected
}

public enum GemProcessProgramSaveStatus
{
    Saved,
    AlreadyExists,
    Rejected
}

public enum GemProcessProgramDeleteStatus
{
    Deleted,
    NotFound,
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
