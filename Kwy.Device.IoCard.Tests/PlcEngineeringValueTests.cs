using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Core;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Core;
using Xunit;

namespace Kwy.Device.IoCard.Tests;

public sealed class PlcEngineeringValueTests
{
    [Fact]
    public void ValueConverter_ConvertsInBothDirections()
    {
        var scale = new PlcValueScale(0, 10000, 0, 10);

        Assert.Equal(5, PlcValueConverter.ToEngineeringValue(5000, scale), 6);
        Assert.Equal(5000, PlcValueConverter.ToRawValue(5, scale), 6);
    }

    [Fact]
    public async Task EngineeringService_ConvertsReadAndWriteWhileRawApiRemainsAvailable()
    {
        var device = new FakePlcDevice { Int16Value = 5000 };
        var service = CreateService(device, ScaledPoint());

        Assert.Equal(5, await service.ReadEngineeringAsync("pressure.value"), 6);
        Assert.Equal((short)5000, await service.ReadAsync<short>("pressure.value"));

        await service.WriteEngineeringAsync("pressure.value", 2.5);

        Assert.Equal((short)2500, device.Int16Value);
    }

    [Fact]
    public async Task EngineeringService_RejectsPointWithoutScale()
    {
        var device = new FakePlcDevice();
        var service = CreateService(device, ScaledPoint() with { Scale = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadEngineeringAsync("pressure.value"));
    }

    [Fact]
    public void Definition_RejectsScaleOnNonNumericPoint()
    {
        PlcPointDefinition point = ScaledPoint() with { DataType = PlcDataType.Boolean };

        Assert.Throws<InvalidOperationException>(point.Validate);
    }

    private static LogicalPlcService CreateService(FakePlcDevice device, PlcPointDefinition point)
        => new(new PlcPointDefinitionProvider([point]), new DeviceRegistry([device]));

    private static PlcPointDefinition ScaledPoint() => new()
    {
        Id = "pressure.value",
        Name = "压力值",
        DeviceId = "plc-1",
        Address = "D100",
        DataType = PlcDataType.Int16,
        Scale = new(0, 10000, 0, 10),
        Unit = "MPa"
    };

    private sealed class FakePlcDevice : IPlcDevice
    {
        public string DeviceId => "plc-1";
        public string DeviceName => "Fake PLC";
        public bool IsConnected => true;
        public ConnectionState State => ConnectionState.Connected;
        public IDeviceConfig DeviceParameter { get; set; } = new FakeConfig();
        public short Int16Value { get; set; }
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
        public event EventHandler<DeviceOperationEventArgs>? OperationOccurred { add { } remove { } }
        public Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default) => Task.FromResult(Int16Value);
        public Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(0f);
        public Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default) => Task.FromResult(new byte[length]);
        public Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(new short[count]);
        public Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(new int[count]);
        public Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(new float[count]);
        public Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default) { Int16Value = value; return Task.CompletedTask; }
        public Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private sealed class FakeConfig : IDeviceConfig { public bool Validate() => true; }
    }
}
