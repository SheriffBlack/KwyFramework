using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.Core.IO;

/// <summary>
/// 物理 IO 卡适配器基类。
/// 仅封装通道校验、端口掩码写入、普通软件定时脉冲和可选硬件中断；不保存业务点位名称或工艺规则。
/// </summary>
public abstract class IoCardBase : DeviceBase, IIoCardDevice, IHardwareInterruptSource
{
    protected const int DefaultIoChannelCount = IoChannelGuard.MaxChannelCount;

    private readonly PulseOutputScheduler pulseScheduler;

    protected IoCardBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
        pulseScheduler = new PulseOutputScheduler(
            WriteDoBit,
            () => !disposed && IsConnected,
            (channel, ex) => RaiseErrorOccurred($"Reset DO pulse channel {channel} failed: {ex.Message}", ex));
    }

    // 具体厂商驱动负责单点读写和端口读取；基类提供通用的掩码操作兜底实现。

    public abstract void WriteDoBit(int channel, bool state);

    public virtual void WriteDoPortMask(ulong mask)
    {
        WriteDoPortMask(mask, IoBitConverter.CreateWritableMask(GetDigitalOutputChannelCount()));
    }

    public virtual void WriteDoPortMask(ulong mask, ulong changedMask)
    {
        int channelCount = GetDigitalOutputChannelCount();
        ulong writableMask = IoBitConverter.CreateWritableMask(channelCount);
        changedMask &= writableMask;

        for (int channel = 0; channel < channelCount; channel++)
        {
            if ((changedMask & (1UL << channel)) != 0)
            {
                WriteDoBit(channel, (mask & (1UL << channel)) != 0);
            }
        }
    }

    protected virtual int GetDigitalOutputChannelCount()
    {
        return DefaultIoChannelCount;
    }

    protected virtual int GetDigitalInputChannelCount()
    {
        return DefaultIoChannelCount;
    }

    public int DigitalInputCount => GetDigitalInputChannelCount();

    public int DigitalOutputCount => GetDigitalOutputChannelCount();

    public virtual void WritePulse(int channel, int durationMs)
    {
        ThrowIfDisposed();
        IoChannelGuard.ValidateChannel(channel, GetDigitalOutputChannelCount(), nameof(channel));
        pulseScheduler.WritePulse(channel, durationMs);
    }

    public abstract bool ReadDiBit(int channel);
    public abstract bool[] ReadAllDi();
    public abstract bool[] ReadAllDo();

    // 所有驱动均以 64 位物理输入快照作为监视器的统一输入。
    public abstract ulong ReadDiPortMask();

    /// <summary>
    /// 驱动收到厂商硬件中断时发布，携带最多 64 位的物理 IO 快照。
    /// </summary>
    public event EventHandler<IoSignalSnapshot>? HardwareInterruptReceived;

    protected void RaiseHardwareInterrupt(ulong mask, IoTriggerEdge? triggerEdge = null)
    {
        HardwareInterruptReceived?.Invoke(
            this,
            new IoSignalSnapshot(DeviceId, mask, DateTimeOffset.UtcNow, IoSnapshotSource.HardwareInterrupt, triggerEdge));
    }



    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        pulseScheduler.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
