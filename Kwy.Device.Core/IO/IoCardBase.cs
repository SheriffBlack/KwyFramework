using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.Core.IO;

/// <summary>
/// IO 板卡设备抽象基类
/// </summary>
public abstract class IoCardBase : DeviceBase, IIoCardDevice, IHardwareInterruptSource, IIoPointRegistry
{
    protected const int DefaultIoChannelCount = IoChannelGuard.MaxChannelCount;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _doNames = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _diNames = new();
    private readonly PulseOutputScheduler pulseScheduler;

    protected IoCardBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
        pulseScheduler = new PulseOutputScheduler(
            WriteDoBit,
            () => !disposed && IsConnected,
            (channel, ex) => RaiseErrorOccurred($"Reset DO pulse channel {channel} failed: {ex.Message}", ex));
    }

    // ==========================================
    // IIoCardDevice 接口实现 (交由子类具体实现)
    // ==========================================

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

    // 🌟 接口对齐：使用 ReadDiPortMask
    public abstract ulong ReadDiPortMask();

    /// <summary>
    /// 当硬件中断触发时抛出，携带最高 64 位 IO 快照
    /// </summary>
    public event EventHandler<IoSignalSnapshot>? HardwareInterruptReceived;

    protected void RaiseHardwareInterrupt(ulong mask, IoTriggerEdge? triggerEdge = null)
    {
        HardwareInterruptReceived?.Invoke(
            this,
            new IoSignalSnapshot(DeviceId, mask, DateTimeOffset.UtcNow, IoSnapshotSource.HardwareInterrupt, triggerEdge));
    }



    // ==========================================
    // 元数据扩展实现
    // ==========================================

    public void SetDoName(int channel, string name)
    {
        IoChannelGuard.ValidateChannel(channel, GetDigitalOutputChannelCount(), nameof(channel));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("DO name cannot be empty.", nameof(name));
        }

        _doNames[channel] = name;
    }

    public IEnumerable<(int Index, string Name)> GetAllOutputs()
        => _doNames.OrderBy(static item => item.Key)
            .Select(static item => (item.Key, item.Value))
            .ToArray();

    public void SetDiName(int channel, string name)
    {
        IoChannelGuard.ValidateChannel(channel, GetDigitalInputChannelCount(), nameof(channel));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("DI name cannot be empty.", nameof(name));
        }

        _diNames[channel] = name;
    }

    public IEnumerable<(int Index, string Name)> GetAllInputs()
        => _diNames.OrderBy(static item => item.Key)
            .Select(static item => (item.Key, item.Value))
            .ToArray();

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
