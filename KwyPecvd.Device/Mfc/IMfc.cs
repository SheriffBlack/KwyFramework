namespace KwyPecvd.Device.Mfc;

public interface IMfc : IHardwareComponent
{
    MfcDefinition Definition { get; }

    MfcState State { get; }

    event EventHandler<MfcState>? StateChanged;

    /// <summary>
    /// 接受新的流量目标，并启动斜坡。
    /// 返回成功仅代表命令被设备层接受。
    /// </summary>
    Task<CommandResult> SetFlowAsync(
        double target,
        TimeSpan rampDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将流量设为零。
    /// 停止 MFC 的工艺输出
    /// </summary>
    Task<CommandResult> StopAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止斜坡并保持当前设定
    /// </summary>
    /// <returns></returns>
    Task<CommandResult> HoldAsync(
        CancellationToken cancellationToken = default);
}