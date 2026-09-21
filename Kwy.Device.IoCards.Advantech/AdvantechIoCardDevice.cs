using Kwy.Device.Abstractions.IO;
using Kwy.Device.Core.IO;

namespace Kwy.Device.IoCards.Advantech;

/// <summary>
/// Advantech PCI/DAQNavi digital IO card implementation.
/// </summary>
public sealed class AdvantechIoCardDevice : IoCardBase
{
    private readonly AdvantechIoCardConfig config;
    private readonly IAdvantechIoSdkPort sdkPort;
    private readonly SemaphoreSlim ioSemaphore = new(1, 1);
    private readonly object interruptSync = new();
    private byte[] diPortBuffer = Array.Empty<byte>();
    private byte[] doPortBuffer = Array.Empty<byte>();
    private volatile bool connected;
    private volatile bool shuttingDown;
    private static readonly TimeSpan NativeReleaseWaitTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IoOperationWaitTimeout = TimeSpan.FromMilliseconds(100);
    private int nativeResourcesReleased;

    public AdvantechIoCardDevice(AdvantechIoCardConfig config)
        : this(config.DeviceDescription, config.Model, config, new DaqNaviAdvantechIoSdkPort())
    {
    }

    public AdvantechIoCardDevice(string deviceId, string deviceName, AdvantechIoCardConfig config)
        : this(deviceId, deviceName, config, new DaqNaviAdvantechIoSdkPort())
    {
    }

    /// <summary>允许测试或宿主注入 1730U 的 SDK 适配实现。</summary>
    public AdvantechIoCardDevice(string deviceId, string deviceName, AdvantechIoCardConfig config, IAdvantechIoSdkPort sdkPort)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Advantech IO card configuration.", nameof(config));
        }

        this.sdkPort = sdkPort ?? throw new ArgumentNullException(nameof(sdkPort));
    }

    public override string DeviceModel => config.Model;

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        shuttingDown = false;
        sdkPort.Open(config.DeviceDescription);

        if (config.EnableInterrupt)
        {
            ConfigureInterrupt();
            sdkPort.DiInterruptReceived -= OnSdkPortInterrupt;
            sdkPort.DiInterruptReceived += OnSdkPortInterrupt;
            try
            {
                sdkPort.StartDiInterrupt();
            }
            catch
            {
                sdkPort.DiInterruptReceived -= OnSdkPortInterrupt;
                throw;
            }
        }

        connected = true;
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        shuttingDown = true;
        connected = false;

        if (config.EnableInterrupt)
        {
            try
            {
                sdkPort.StopDiInterrupt();
            }
            catch
            {
            }

            sdkPort.DiInterruptReceived -= OnSdkPortInterrupt;
        }

        sdkPort.Close();

        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive()
    {
        return connected;
    }

    public override void WriteDoBit(int channel, bool state)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, GetDoChannelCount(), nameof(channel));

        ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            sdkPort.WriteDoBit(port, bit, state);
        });
    }

    public override void WriteDoPortMask(ulong mask)
    {
        EnsureReady();
        WriteDoPortMaskCore(mask, GetWritableDoMask());
    }

    public override void WriteDoPortMask(ulong mask, ulong changedMask)
    {
        EnsureReady();
        WriteDoPortMaskCore(mask, changedMask & GetWritableDoMask());
    }

    public override bool ReadDiBit(int channel)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, GetDiChannelCount(), nameof(channel));

        return ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            return sdkPort.ReadDiBit(port, bit);
        });
    }

    public override bool[] ReadAllDi()
    {
        EnsureReady();
        return ReadDiPorts(GetDiPortCount());
    }

    public override bool[] ReadAllDo()
    {
        EnsureReady();
        return ReadDoPorts(GetDoPortCount());
    }

    public override ulong ReadDiPortMask()
    {
        EnsureReady();
        return ReadDiPortMaskCore();
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed && Volatile.Read(ref nativeResourcesReleased) != 0)
        {
            return;
        }

        shuttingDown = true;
        connected = false;

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (State == Kwy.Communicate.Abstractions.Enums.ConnectionState.Disconnected)
                ReleaseNativeResources();
            if (Volatile.Read(ref nativeResourcesReleased) != 0)
                GC.SuppressFinalize(this);
        }
    }
    public override void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();

    ~AdvantechIoCardDevice()
    {
        ReleaseNativeResources();
    }

    private void ReleaseNativeResources()
    {
        if (Volatile.Read(ref nativeResourcesReleased) != 0)
        {
            return;
        }

        connected = false;
        shuttingDown = true;

        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(NativeReleaseWaitTimeout);
            if (!lockTaken)
                return;
            if (Interlocked.Exchange(ref nativeResourcesReleased, 1) != 0)
                return;
            ReleaseNativeResourcesCore();
        }
        catch (ObjectDisposedException)
        {
            if (Interlocked.Exchange(ref nativeResourcesReleased, 1) == 0)
                ReleaseNativeResourcesCore();
        }
        finally
        {
            if (lockTaken)
            {
                try
                {
                    ioSemaphore.Release();
                }
                catch
                {
                }

                try
                {
                    ioSemaphore.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    private void ReleaseNativeResourcesCore()
    {
        try
        {
            sdkPort.DiInterruptReceived -= OnSdkPortInterrupt;
        }
        catch
        {
        }

        if (config.EnableInterrupt)
        {
            try
            {
                sdkPort.StopDiInterrupt();
            }
            catch
            {
            }
        }

        try
        {
            sdkPort.Dispose();
        }
        catch
        {
        }
    }

    private ulong ReadDiPortMaskCore()
    {
        return ExecuteIo(() =>
        {
            int portCount = GetDiPortCount();
            byte[] portData = GetDiPortBuffer(portCount);
            sdkPort.ReadDiPorts(portData, portCount);

            return IoBitConverter.ToMask(portData);
        });
    }

    private void ConfigureInterrupt()
    {
        lock (interruptSync)
        {
            IoChannelGuard.ValidateChannel(config.InterruptChannel, GetDiChannelCount(), nameof(config.InterruptChannel));
            int interruptIndex = config.InterruptChannel / 8;
            if (interruptIndex >= sdkPort.DigitalInputPortCount)
            {
                throw new NotSupportedException($"Advantech interrupt channel {config.InterruptChannel} is not supported by this device.");
            }

            sdkPort.ConfigureDiInterrupt(interruptIndex, config.InterruptRisingEdge
                ? IoTriggerEdge.Rising
                : IoTriggerEdge.Falling);
        }
    }

    private bool[] ReadDiPorts(int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDiPortBuffer(portCount);
            sdkPort.ReadDiPorts(portData, portCount);
            return IoBitConverter.ToBits(portData);
        });
    }

    private bool[] ReadDoPorts(int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDoPortBuffer(portCount);
            sdkPort.ReadDoPorts(portData, portCount);
            return IoBitConverter.ToBits(portData);
        });
    }

    private void WriteDoPortMaskCore(ulong mask, ulong changedMask)
    {
        if (changedMask == 0)
        {
            return;
        }

        ExecuteIo(() =>
        {
            int portCount = GetDoPortCount();
            byte[] currentData = GetDoPortBuffer(portCount);
            sdkPort.ReadDoPorts(currentData, portCount);

            for (int port = 0; port < portCount; port++)
            {
                byte targetValue = currentData[port];
                for (int bit = 0; bit < 8; bit++)
                {
                    int channel = port * 8 + bit;
                    ulong bitMask = 1UL << channel;
                    if ((changedMask & bitMask) == 0)
                    {
                        continue;
                    }

                    if ((mask & bitMask) != 0)
                    {
                        targetValue = (byte)(targetValue | (1 << bit));
                    }
                    else
                    {
                        targetValue = (byte)(targetValue & ~(1 << bit));
                    }
                }

                if (targetValue != currentData[port])
                {
                    sdkPort.WriteDoPort(port, targetValue);
                }
            }
        });
    }

    private byte[] GetDiPortBuffer(int portCount)
    {
        if (diPortBuffer.Length != portCount)
        {
            diPortBuffer = new byte[portCount];
        }

        return diPortBuffer;
    }

    private byte[] GetDoPortBuffer(int portCount)
    {
        if (doPortBuffer.Length != portCount)
        {
            doPortBuffer = new byte[portCount];
        }

        return doPortBuffer;
    }

    private T ExecuteIo<T>(Func<T> operation)
    {
        ThrowIfUnavailable();
        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(IoOperationWaitTimeout);
            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for Advantech IO operation lock.");
            }

            ThrowIfUnavailable();
            return operation();
        }
        catch (Exception exception)
        {
            RaiseErrorOccurred($"[{DeviceName}/{DeviceId}] Advantech IO operation failed: {exception.Message}", exception);
            throw;
        }
        finally
        {
            if (lockTaken)
            {
                ioSemaphore.Release();
            }
        }
    }
    private void ExecuteIo(Action operation)
    {
        ThrowIfUnavailable();
        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(IoOperationWaitTimeout);
            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for Advantech IO operation lock.");
            }

            ThrowIfUnavailable();
            operation();
        }
        catch (Exception exception)
        {
            RaiseErrorOccurred($"[{DeviceName}/{DeviceId}] Advantech IO operation failed: {exception.Message}", exception);
            throw;
        }
        finally
        {
            if (lockTaken)
            {
                ioSemaphore.Release();
            }
        }
    }
    private void OnSdkPortInterrupt(object? sender, int interruptPort)
    {
        if (interruptPort == config.InterruptChannel / 8)
        {
            ThreadPool.QueueUserWorkItem(_ => PublishHardwareTriggerSnapshot());
        }
    }

    private void PublishHardwareTriggerSnapshot()
    {
        try
        {
            RaiseHardwareInterrupt(
                ReadDiPortMaskCore(),
                config.InterruptRisingEdge ? IoTriggerEdge.Rising : IoTriggerEdge.Falling);
        }
        catch (Exception ex)
        {
            if (!shuttingDown)
            {
                RaiseErrorOccurred($"Read Advantech DI snapshot after interrupt failed: {ex.Message}", ex);
            }
        }
    }

    private int GetDiPortCount()
    {
        return Math.Min(config.DiPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, sdkPort.DigitalInputPortCount));
    }

    private int GetDoPortCount()
    {
        return Math.Min(config.DoPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, sdkPort.DigitalOutputPortCount));
    }

    private int GetDiChannelCount()
    {
        return GetDiPortCount() * 8;
    }

    private int GetDoChannelCount()
    {
        return GetDoPortCount() * 8;
    }

    private ulong GetWritableDoMask()
    {
        return IoBitConverter.CreateWritableMask(GetDoChannelCount());
    }

    protected override int GetDigitalOutputChannelCount()
    {
        return GetDoChannelCount();
    }

    protected override int GetDigitalInputChannelCount()
    {
        return GetDiChannelCount();
    }

    private void EnsureReady()
    {
        ThrowIfDisposed();
        ThrowIfUnavailable();
    }

    private void ThrowIfUnavailable()
    {
        if (shuttingDown || !IsConnected)
        {
            throw new InvalidOperationException("Advantech IO card is not connected.");
        }
    }

}
