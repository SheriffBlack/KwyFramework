namespace Kwy.Communicate.Gem;

/// <summary>
/// 定义设备侧 GEM 会话的高层操作。
/// </summary>
/// <remarks>
/// 用于协调 GEM 通信状态、控制状态以及事件、报警、Process Program、Trace 和远程命令等设备侧行为。
///
/// 本接口不表示具体物理设备，也不负责 PLC 控制、工艺流程、安全互锁或具体设备的 GEM 接口编号定义。
/// </remarks>
public interface IGemEquipmentSession : IDisposable
{
    GemSessionStateMachine StateMachine { get; }

    GemCommunicationState CommunicationState { get; }

    GemControlState ControlState { get; }

    GemCommunicationContext Context { get; }

    int SpooledMessageCount { get; }

    /// <summary>连接 HSMS 并通过 S1F13/S1F14 建立 GEM 通信。</summary>
    Task EstablishCommunicationAsync(CancellationToken cancellationToken = default);

    /// <summary>接受对端发起的 S1F13，并将会话标记为 Communicating。</summary>
    Task AcceptCommunicationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认 Host 可通信后进入设备侧 Online Local 或 Online Remote 状态。
    /// </summary>
    /// <remarks>
    /// 该操作不会代替对 Host S1F17 Request ON-LINE 的处理；具体项目仍需根据客户接口
    /// 实现 S1F17/S1F18 Handler、权限与设备安全状态检查。
    /// </remarks>
    Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default);

    /// <summary>在业务授权通过后，接受 Host 发起的 S1F17 Request ON-LINE。</summary>
    Task AcceptHostOnlineRequestAsync(bool remote, CancellationToken cancellationToken = default);

    Task SetOfflineAsync(CancellationToken cancellationToken = default);

    Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default);

    Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default);

    Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default);

    /// <summary>通过已配置的 <see cref="IGemProcessProgramRepository"/> 保存 Process Program。</summary>
    Task<GemProcessProgramSaveResult> SaveProcessProgramAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<GemProcessProgram?> FindProcessProgramAsync(
        string ppid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListProcessProgramIdsAsync(
        CancellationToken cancellationToken = default);

    Task<GemProcessProgramDeleteResult> DeleteProcessProgramAsync(
        string ppid,
        CancellationToken cancellationToken = default);

    /// <summary>采集单个 Trace 快照，但不自动发送。</summary>
    Task<GemTraceSample> CaptureTraceAsync(uint traceId, uint sampleNumber, CancellationToken cancellationToken = default);

    /// <summary>按 Trace 定义的采样间隔和次数运行采样，并逐条发送 S6F1。</summary>
    Task<GemTraceRunResult> RunTraceAsync(uint traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行已注册的设备业务命令，并在真实执行结束后返回最终结果。
    /// </summary>
    /// <remarks>
    /// 当前标准 S2F41 Handler 会等待本方法返回后发送 S2F42，因此处理器必须在 T3 内完成。
    /// 耗时动作应由设备应用层先生成自有命令快照并完成入队，再返回 Accepted。
    /// </remarks>
    Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(
        GemRemoteCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 Remote Command 定义的 Accepted/Completed/Failed CEID 上报业务执行状态。
    /// </summary>
    /// <returns>存在对应 CEID 并已发送时为 true；该状态未配置 CEID 时为 false。</returns>
    Task<bool> ReportRemoteCommandStatusAsync(
        string commandName,
        GemRemoteCommandCompletionStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>在已恢复 GEM Communicating 后按序补传 Spool。</summary>
    Task<GemSpoolReplayResult> ReplaySpooledMessagesAsync(
        int maximumMessages = int.MaxValue,
        CancellationToken cancellationToken = default);

    /// <summary>重建 S1F13/S1F14 通信并按序补传；不会自动恢复 Online Remote。</summary>
    Task<GemSpoolReplayResult> RecoverCommunicationAsync(
        int maximumMessages = int.MaxValue,
        CancellationToken cancellationToken = default);
}
