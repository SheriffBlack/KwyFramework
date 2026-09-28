namespace Kwy.Device.Abstractions.IO;

/// <summary>
/// 物理数字输入能力，面向卡适配器和维护诊断。
/// 参数是硬件通道号，不处理 IoPointDefinition 的 Id、反相或工艺规则；业务流程不应直接注入此接口。
/// </summary>
public interface IDigitalInputDevice
{
    /// <summary>读取指定物理 DI 通道的当前电平，不应用逻辑反相。</summary>
    bool ReadDiBit(int channel);
    /// <summary>读取设备全部物理 DI 通道；数组下标即物理通道号。</summary>
    bool[] ReadAllDi();
    /// <summary>读取最多 64 个物理 DI 通道的位快照。</summary>
    ulong ReadDiPortMask();
}

/// <summary>
/// 物理数字输出能力，面向卡适配器和维护诊断。
/// 参数是硬件通道号，不处理 IoPointDefinition 的 Owner、Inverted、ProcessSafeState；业务流程应改用 ILogicalIoWriter。
/// </summary>
public interface IDigitalOutputDevice
{
    /// <summary>写入指定物理 DO 通道，不应用逻辑反相或 Owner 校验。</summary>
    void WriteDoBit(int channel, bool state);
    /// <summary>按掩码全量写入物理 DO 端口。</summary>
    void WriteDoPortMask(ulong mask);
    /// <summary>仅修改 <paramref name="changedMask"/> 指定的物理 DO 通道。</summary>
    void WriteDoPortMask(ulong mask, ulong changedMask);
    /// <summary>读取设备全部物理 DO 通道；数组下标即物理通道号。</summary>
    bool[] ReadAllDo();
}

/// <summary>物理 IO 卡的实际通道能力；当前公共模型最多支持 64 点。</summary>
public interface IIoChannelCapabilities
{
    /// <summary>设备实际可用的 DI 通道数量；配置初始化时用于校验 IoPointDefinition.Channel。</summary>
    int DigitalInputCount { get; }

    /// <summary>设备实际可用的 DO 通道数量；配置初始化时用于校验 IoPointDefinition.Channel。</summary>
    int DigitalOutputCount { get; }
}

/// <summary>DI 快照的采集来源；用于区分轮询结果与硬件中断结果。</summary>
public enum IoSnapshotSource
{
    Polling,
    HardwareInterrupt
}

/// <summary>硬件中断的触发沿；轮询快照通常为 null。</summary>
public enum IoTriggerEdge
{
    Rising,
    Falling
}

/// <summary>一次物理 DI 快照及其采集上下文。</summary>
public sealed record IoSignalSnapshot(
    string DeviceId,
    ulong Mask,
    DateTimeOffset Timestamp,
    IoSnapshotSource Source,
    IoTriggerEdge? TriggerEdge = null);

/// <summary>可选硬件中断能力，面向监视器和快速硬件互锁观察，不替代安全回路。</summary>
public interface IHardwareInterruptSource
{
    event EventHandler<IoSignalSnapshot>? HardwareInterruptReceived;
}

/// <summary>等待逻辑 DI 的硬件中断通知。未具备硬件中断能力的设备应明确拒绝此调用。</summary>
public interface ILogicalIoInterruptWaiter
{
    Task WaitForInputInterruptAsync(
        string pointId,
        bool expectedState,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 业务逻辑输入读取能力。参数为 IoPointDefinition.Id，而非物理通道号；读取结果已应用 Inverted。
/// 流程、工艺和站点应注入此接口，而不是 IDigitalInputDevice。
/// </summary>
public interface ILogicalIoReader
{
    /// <summary>读取已采集的逻辑 DI 状态；未采集到快照时抛出异常。</summary>
    bool ReadDi(string pointId);
    /// <summary>尝试读取已采集的逻辑 DI 状态；未知点位或无快照时返回 <see langword="false"/>。</summary>
    bool TryReadDi(string pointId, out bool state);
    /// <summary>返回监视器已采集的逻辑 DI 快照；此调用不触发硬件读取。</summary>
    IReadOnlyDictionary<string, bool> GetCapturedDiStates();
}

/// <summary>
/// 业务逻辑输出写入能力。参数为 IoPointDefinition.Id；实现会校验 Owner、应用 Inverted 并定位物理设备与通道。
/// 流程、工艺和站点应注入此接口，而不是 IDigitalOutputDevice。
/// </summary>
public interface ILogicalIoWriter
{
    /// <summary>按逻辑点位 ID 写入 DO，并校验该点位的 Owner。</summary>
    void WriteDo(string pointId, bool state, string? owner = null);
    /// <summary>
    /// 输出普通软件定时脉冲。计时受 Windows 调度影响，不能用于功能安全、飞拍或其他实时触发。
    /// </summary>
    void WriteTimedPulse(string pointId, int durationMs, string? owner = null);
}

/// <summary>工艺输出收敛能力，不承担功能安全职责。</summary>
public interface IProcessOutputStateController
{
    /// <summary>将配置了 ProcessSafeState 的逻辑 DO 写至工艺安全状态；失败时抛出包含点位信息的异常。</summary>
    void ApplyProcessSafeOutputs();
}

/// <summary>逻辑 IO 状态订阅能力；订阅者异常不应阻断监视器扫描。</summary>
public interface IIoStateSubscription
{
    event Action<string, bool>? OnIoStateChanged;
    event Action<IoSignalSnapshot>? OnIoSnapshotReceived;
    event Action<string, Exception>? OnIoReadFailed;
    event Action<string, Exception>? OnIoWriteFailed;
    event Action<string, Exception>? OnIoNotificationFailed;
}

/// <summary>逻辑 IO 的初始化、监视与诊断服务。</summary>
public interface IIoStateMonitor : IIoStateSubscription, IDisposable
{
    /// <summary>加载并校验物理设备与逻辑 IO 点位定义；应在设备流程启动前调用一次。</summary>
    void Initialize(
        IEnumerable<IIoCardDevice> devices,
        IIoPointDefinitionProvider pointDefinitions);

    /// <summary>停止轮询和中断等待，不自动改变输出状态；工艺输出收敛须显式由 IProcessOutputStateController 应用。</summary>
    void Stop();

}

public interface IIoCardDevice :
    IDevice,
    IConfigurableDevice,
    IDigitalInputDevice,
    IDigitalOutputDevice,
    IIoChannelCapabilities
{
}
