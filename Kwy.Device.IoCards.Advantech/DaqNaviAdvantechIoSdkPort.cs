using Automation.BDaq;
using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.IoCards.Advantech;

/// <summary>DAQNavi 的 1730U 数字量适配实现。</summary>
public sealed class DaqNaviAdvantechIoSdkPort : IAdvantechIoSdkPort
{
    private readonly InstantDiCtrl diController = new();
    private readonly InstantDoCtrl doController = new();
    private bool opened;

    public event EventHandler<int>? DiInterruptReceived;

    public int DigitalInputPortCount => diController.Features.PortCount;

    public int DigitalOutputPortCount => doController.Features.PortCount;

    public void Open(string deviceDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceDescription);
        var device = new DeviceInformation(deviceDescription);
        diController.SelectedDevice = device;
        doController.SelectedDevice = device;
        opened = true;
    }

    public void Close()
    {
        StopDiInterrupt();
        opened = false;
    }

    public void ConfigureDiInterrupt(int port, IoTriggerEdge triggerEdge)
    {
        EnsureOpened();
        if ((uint)port >= (uint)diController.DiintChannels.Count())
            throw new NotSupportedException($"Advantech DI interrupt port {port} is not supported.");

        diController.DiintChannels[port].Enabled = true;
        diController.DiintChannels[port].TrigEdge = triggerEdge == IoTriggerEdge.Rising
            ? ActiveSignal.RisingEdge
            : ActiveSignal.FallingEdge;
    }

    public void StartDiInterrupt()
    {
        EnsureOpened();
        diController.Interrupt -= OnInterrupt;
        diController.Interrupt += OnInterrupt;
        ThrowIfFailed(diController.SnapStart(), "Start DI interrupt listener failed");
    }

    public void StopDiInterrupt()
    {
        try { diController.Interrupt -= OnInterrupt; } catch { }
        try { ThrowIfFailed(diController.SnapStop(), "Stop DI interrupt listener failed"); } catch { }
    }

    public bool ReadDiBit(int port, int bit)
    {
        EnsureOpened();
        ThrowIfFailed(diController.ReadBit(port, bit, out byte value), "Read DI bit failed");
        return value != 0;
    }

    public void WriteDoBit(int port, int bit, bool state)
    {
        EnsureOpened();
        ThrowIfFailed(doController.WriteBit(port, bit, state ? (byte)1 : (byte)0), "Write DO bit failed");
    }

    public void ReadDiPorts(byte[] destination, int portCount)
    {
        EnsureOpened();
        ThrowIfFailed(diController.Read(0, portCount, destination), "Read DI ports failed");
    }

    public void ReadDoPorts(byte[] destination, int portCount)
    {
        EnsureOpened();
        ThrowIfFailed(doController.Read(0, portCount, destination), "Read DO ports failed");
    }

    public void WriteDoPort(int port, byte value)
    {
        EnsureOpened();
        ThrowIfFailed(doController.Write(port, value), "Write DO port failed");
    }

    public void Dispose()
    {
        Close();
        try { diController.Cleanup(); } catch { }
        try { doController.Cleanup(); } catch { }
        diController.Dispose();
        doController.Dispose();
    }

    private void OnInterrupt(object? sender, DiSnapEventArgs eventArgs)
        => DiInterruptReceived?.Invoke(this, eventArgs.SrcNum);

    private void EnsureOpened()
    {
        if (!opened)
            throw new InvalidOperationException("Advantech DAQNavi device is not open.");
    }

    private static void ThrowIfFailed(ErrorCode errorCode, string operation)
    {
        if (errorCode != ErrorCode.Success)
            throw new InvalidOperationException($"DAQNavi {operation}: {errorCode}.");
    }
}
