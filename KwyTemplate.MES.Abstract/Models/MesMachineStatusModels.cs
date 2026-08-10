namespace KwyTemplate.MES.Abstract.Models;

public enum MachineRunState
{
    Unknown,
    Idle,
    Running,
    Paused,
    Stopped,
    Alarm,
    Offline
}

public sealed record MesMachineStatusUploadRequest(
    MesRequestContext Context,
    MachineRunState RunState,
    DateTimeOffset Time,
    string? AlarmCode = null,
    string? AlarmMessage = null,
    MesParameterBag? Parameters = null);