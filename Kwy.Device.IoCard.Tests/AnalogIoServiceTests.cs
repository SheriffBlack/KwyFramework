using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Core;
using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.IoCard.Core;
using Xunit;

namespace Kwy.Device.IoCard.Tests;

public sealed class AnalogIoServiceTests
{
    [Fact]
    public void ValueConverter_ConvertsInBothDirections()
    {
        var scale = new AnalogIoScale(4, 20, 0, 10);

        Assert.Equal(5, AnalogIoValueConverter.ToEngineeringValue(12, scale), 6);
        Assert.Equal(12, AnalogIoValueConverter.ToRawValue(5, scale), 6);
    }

    [Fact]
    public void ValueConverter_RejectsInvalidScale()
    {
        var scale = new AnalogIoScale(20, 4, 0, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnalogIoValueConverter.ToEngineeringValue(12, scale));
    }

    [Fact]
    public async Task ReadSampleAsync_ConvertsRawValueToEngineeringValue()
    {
        var device = new FakeAnalogDevice { InputRawValue = 12 };
        var service = CreateService(device, Input());

        AnalogIoSample sample = await service.ReadSampleAsync("pressure.in");

        Assert.Equal(5, sample.Value, 6);
        Assert.Equal(12, sample.RawValue, 6);
        Assert.Equal(AnalogIoQuality.Good, sample.Quality);
    }

    [Theory]
    [InlineData(3.9, AnalogIoQuality.UnderRange)]
    [InlineData(20.1, AnalogIoQuality.OverRange)]
    [InlineData(double.NaN, AnalogIoQuality.Invalid)]
    public async Task ReadSampleAsync_ReportsRawSignalQuality(double rawValue, AnalogIoQuality expected)
    {
        var device = new FakeAnalogDevice { InputRawValue = rawValue };
        var service = CreateService(device, Input());

        AnalogIoSample sample = await service.ReadSampleAsync("pressure.in");

        Assert.Equal(expected, sample.Quality);
    }

    [Fact]
    public async Task WriteAsync_ConvertsEngineeringValueAndChecksOwner()
    {
        var device = new FakeAnalogDevice();
        var service = CreateService(device, Output() with { Owner = "heater" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.WriteAsync("heater.command", 50, "other"));

        await service.WriteAsync("heater.command", 50, "heater");
        Assert.Equal(5, device.LastOutputRawValue, 6);
    }

    [Fact]
    public async Task ApplyProcessSafeOutputsAsync_WritesConfiguredEngineeringValue()
    {
        var device = new FakeAnalogDevice();
        var service = CreateService(device, Output() with { ProcessSafeValue = 10 });

        await service.ApplyProcessSafeOutputsAsync();

        Assert.Equal(1, device.LastOutputRawValue, 6);
    }

    [Fact]
    public void Provider_RejectsDuplicatePhysicalChannelsIgnoringDeviceIdCase()
    {
        Assert.Throws<ArgumentException>(() => new AnalogIoPointDefinitionProvider([
            Input(),
            Input() with { Id = "pressure.other", DeviceId = "ANALOG-1" }
        ]));
    }

    [Fact]
    public void Definition_RejectsChannelOutsideDefaultSixtyFourPoints()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (Input() with { Channel = 64 }).Validate());
    }

    private static LogicalAnalogIoService CreateService(FakeAnalogDevice device, params AnalogIoPointDefinition[] points)
        => new(new AnalogIoPointDefinitionProvider(points), new DeviceRegistry([device]));

    private static AnalogIoPointDefinition Input() => new()
    {
        Id = "pressure.in",
        Name = "压力输入",
        DeviceId = "analog-1",
        Direction = AnalogIoDirection.Input,
        Channel = 0,
        ElectricalSignal = AnalogElectricalSignal.Current,
        Scale = new(4, 20, 0, 10),
        Unit = "MPa"
    };

    private static AnalogIoPointDefinition Output() => new()
    {
        Id = "heater.command",
        Name = "加热功率指令",
        DeviceId = "analog-1",
        Direction = AnalogIoDirection.Output,
        Channel = 0,
        ElectricalSignal = AnalogElectricalSignal.Voltage,
        Scale = new(0, 10, 0, 100),
        Unit = "%"
    };

    private sealed class FakeAnalogDevice : IAnalogInputDevice, IAnalogOutputDevice
    {
        public string DeviceId => "analog-1";
        public string DeviceName => "Fake Analog IO";
        public bool IsConnected => true;
        public ConnectionState State => ConnectionState.Connected;
        public IDeviceConfig DeviceParameter { get; set; } = new FakeConfig();
        public int AnalogInputCount => 1;
        public int AnalogOutputCount => 1;
        public double InputRawValue { get; init; }
        public double LastOutputRawValue { get; private set; }
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
        public event EventHandler<DeviceOperationEventArgs>? OperationOccurred { add { } remove { } }
        public ValueTask<double> ReadAnalogInputRawAsync(int channel, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InputRawValue);
        public ValueTask WriteAnalogOutputRawAsync(int channel, double rawValue, CancellationToken cancellationToken = default)
        {
            LastOutputRawValue = rawValue;
            return ValueTask.CompletedTask;
        }
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private sealed class FakeConfig : IDeviceConfig { public bool Validate() => true; }
    }
}
