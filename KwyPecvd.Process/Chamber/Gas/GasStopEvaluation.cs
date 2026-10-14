namespace KwyPecvd.Process.Chamber.Gas;

internal enum GasStopStatus
{
    /// <summary>停止命令已经发出，但反馈还没有归零。</summary>
    Waiting,
    /// <summary>全部气路反馈已经归零。 </summary>
    Stopped,
    /// <summary>反馈无效、MFC 离线等，无法确认安全停止。 </summary>
    Faulted
}

internal sealed record GasStopEvaluation(
    GasStopStatus Status,
    string? FaultCode = null,
    string? FaultMessage = null);