namespace Kwy.Device.Abstractions.IO;

/// <summary>
/// 物理数字输入能力，面向卡适配器和维护诊断。
/// 参数是硬件通道号，不处理 IoPoint 的 Id、反相或工艺规则；业务流程不应直接注入此接口。
/// </summary>
public interface IDigitalInputDevice
{
    bool ReadDiBit(int channel);
    bool[] ReadAllDi();
    ulong ReadDiPortMask();
}

/// <summary>
/// 物理数字输出能力，面向卡适配器和维护诊断。
/// 参数是硬件通道号，不处理 IoPoint 的 Owner、Inverted、SafeState；业务流程应改用 ILogicalIoWriter。
/// </summary>
public interface IDigitalOutputDevice
{
    void WriteDoBit(int channel, bool state);
    void WriteDoPortMask(ulong mask);
    void WriteDoPortMask(ulong mask, ulong changedMask);
    bool[] ReadAllDo();
}

/// <summary>物理 IO 卡的实际通道能力；当前公共模型最多支持 64 点。</summary>
public interface IIoChannelCapabilities
{
    /// <summary>设备实际可用的 DI 通道数量；配置初始化时用于校验 IoPoint.Channel。</summary>
    int DigitalInputCount { get; }

    /// <summary>设备实际可用的 DO 通道数量；配置初始化时用于校验 IoPoint.Channel。</summary>
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

/// <summary>等待指定物理 DI 的硬件中断；参数为物理通道，仅供底层控制逻辑使用。</summary>
public interface IHardwareInterruptWaiter
{
    Task WaitForHardwareInterruptAsync(
        IIoCardDevice device,
        int channel,
        bool expectedState,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 业务层使用的完整逻辑 IO 服务；通过稳定 pointId 访问点位，处理极性、Owner 与安全状态。
/// 例如 WriteDo("vacuum.valve", true) 最终才会映射为某张卡的物理通道写入。
/// </summary>
public interface ILogicalIoService
{
    bool ReadDi(string pointId);
    bool TryReadDi(string pointId, out bool state);
    void WriteDo(string pointId, bool state, string? owner = null);
    void WritePulse(string pointId, int durationMs, string? owner = null);
    void ApplySafeOutputs();

    IReadOnlyDictionary<string, bool> RefreshAllDi();
}

/// <summary>
/// 业务逻辑输入读取能力。参数为 IoPoint.Id，而非物理通道号；读取结果已应用 Inverted。
/// 流程、工艺和站点应注入此接口，而不是 IDigitalInputDevice。
/// </summary>
public interface ILogicalIoReader
{
    bool ReadDi(string pointId);
    bool TryReadDi(string pointId, out bool state);
    IReadOnlyDictionary<string, bool> RefreshAllDi();
}

/// <summary>
/// 业务逻辑输出写入能力。参数为 IoPoint.Id；实现会校验 Owner、应用 Inverted 并定位物理设备与通道。
/// 流程、工艺和站点应注入此接口，而不是 IDigitalOutputDevice。
/// </summary>
public interface ILogicalIoWriter
{
    void WriteDo(string pointId, bool state, string? owner = null);
    void WritePulse(string pointId, int durationMs, string? owner = null);
}

/// <summary>安全输出应用能力。</summary>
public interface IIoSafetyController
{
    /// <summary>将配置了 SafeState 的逻辑 DO 写至安全状态；失败时应抛出包含点位信息的异常。</summary>
    void ApplySafeOutputs();
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
public interface IIoStateMonitor : ILogicalIoService, ILogicalIoReader, ILogicalIoWriter, IIoSafetyController, IIoStateSubscription, IHardwareInterruptWaiter, IDisposable
{
    /// <summary>DI 轮询周期（毫秒）；仅影响无硬件中断或补充确认的场景。</summary>
    int PollingIntervalMs { get; set; }

    /// <summary>加载并校验物理设备、DI 点位和 DO 点位映射；应在设备流程启动前调用一次。</summary>
    void Initialize(
        IEnumerable<IIoCardDevice> devices,
        IEnumerable<IoPoint> diConfigs,
        IEnumerable<IoPoint> doConfigs);

    /// <summary>停止轮询和中断等待，不自动改变输出状态；安全输出须显式由 IIoSafetyController 应用。</summary>
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
