using Kwy.Device.Abstractions.IO;
using Kwy.Device.IoCards.Advantech;
using Xunit;

namespace Kwy.Device.Io.Tests;

/// <summary>验证 1730U 驱动只依赖窄 SDK Port，而非 DAQNavi 具体类型。</summary>
public sealed class AdvantechIoSdkPortContractTests
{
    [Fact]
    public async Task Device_MapsReadWriteAndInterruptThroughSdkPort()
    {
        var port = new Fake1730UPort();
        var config = new AdvantechIoCardConfig
        {
            DiPortCount = 1,
            DoPortCount = 1,
            EnableInterrupt = true,
            InterruptChannel = 0,
            InterruptRisingEdge = true
        };
        await using var device = new AdvantechIoCardDevice("io.1730u", "1730U", config, port);
        await device.ConnectAsync();

        device.WriteDoBit(3, true);
        Assert.Equal((byte)0b0000_1000, port.DoPorts[0]);
        port.DiPorts[0] = 0b0000_0010;
        Assert.True(device.ReadDiBit(1));

        IoSignalSnapshot? snapshot = null;
        device.HardwareInterruptReceived += (_, value) => snapshot = value;
        port.RaiseInterrupt(0);
        await Task.Delay(50);

        Assert.True(port.Opened);
        Assert.True(port.InterruptStarted);
        Assert.NotNull(snapshot);
        Assert.Equal("io.1730u", snapshot!.DeviceId);
        Assert.Equal(IoTriggerEdge.Rising, snapshot.TriggerEdge);
        Assert.Equal((ulong)0b10, snapshot.Mask);
    }

    private sealed class Fake1730UPort : IAdvantechIoSdkPort
    {
        public event EventHandler<int>? DiInterruptReceived;
        public int DigitalInputPortCount => 1;
        public int DigitalOutputPortCount => 1;
        public bool Opened { get; private set; }
        public bool InterruptStarted { get; private set; }
        public byte[] DiPorts { get; } = new byte[1];
        public byte[] DoPorts { get; } = new byte[1];

        public void Open(string deviceDescription) => Opened = true;
        public void Close() => Opened = false;
        public void ConfigureDiInterrupt(int port, IoTriggerEdge triggerEdge) { }
        public void StartDiInterrupt() => InterruptStarted = true;
        public void StopDiInterrupt() => InterruptStarted = false;
        public bool ReadDiBit(int port, int bit) => (DiPorts[port] & (1 << bit)) != 0;
        public void WriteDoBit(int port, int bit, bool state)
        {
            DoPorts[port] = state ? (byte)(DoPorts[port] | (1 << bit)) : (byte)(DoPorts[port] & ~(1 << bit));
        }
        public void ReadDiPorts(byte[] destination, int portCount) => Array.Copy(DiPorts, destination, portCount);
        public void ReadDoPorts(byte[] destination, int portCount) => Array.Copy(DoPorts, destination, portCount);
        public void WriteDoPort(int port, byte value) => DoPorts[port] = value;
        public void RaiseInterrupt(int port) => DiInterruptReceived?.Invoke(this, port);
        public void Dispose() { }
    }
}
