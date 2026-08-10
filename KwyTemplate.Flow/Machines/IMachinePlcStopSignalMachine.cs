namespace KwyTemplate.Flow.Machines;


/// <summary>
/// 读取到某型号，机台停止
/// </summary>


public enum MachinePlcStopSignalKind
{
    TapeMotorRelease,
    CheckExpiredReelCompleted,
    StandardExpiredReelCompleted
}

public sealed record MachinePlcStopSignal(
    MachinePlcStopSignalKind Kind,
    bool ClearTablePaperCode,
    bool ResetAfterHandled);

public interface IMachinePlcStopSignalMachine
{
    Task<IReadOnlyList<MachinePlcStopSignal>> ReadPlcStopSignalsAsync(CancellationToken cancellationToken = default);

    Task ResetPlcStopSignalAsync(MachinePlcStopSignalKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges a successfully completed check to the PLC.
    /// </summary>
    Task SetCheckStopSignalsCompletedAsync(CancellationToken cancellationToken = default);
}
