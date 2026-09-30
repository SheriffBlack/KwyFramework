using Kwy.Communicate.FMdb;
using Kwy.Communicate.FMdb.Enums;

namespace Kwy.Communicate.Tests;

public sealed class FMdbCommunicationTests
{
    [Fact]
    public void Constructor_RejectsInvalidEnumConfiguration()
    {
        var config = new MdbConfig { Transport = (MdbTransport)99 };

        Assert.Throws<ArgumentException>(() => new FMdbCommunication(config));
    }

    [Fact]
    public async Task ReadCoils_RejectsProtocolQuantityBeforeConnectionCheck()
    {
        await using var client = new FMdbCommunication(new MdbConfig());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadCoilsAsync(0, 2001));
    }

    [Fact]
    public async Task TypedRegisterRead_ValidatesRegisterCountAndAddressEnd()
    {
        await using var client = new FMdbCommunication(new MdbConfig());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadHoldingRegistersAsync<uint>(0, 63));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadHoldingRegistersAsync<ushort>(65535, 2));
    }

    [Fact]
    public async Task Constructor_TakesConfigurationSnapshot()
    {
        var config = new MdbConfig
        {
            Transport = MdbTransport.Rtu,
            SerialPort = "__KWY_TEST_MISSING_PORT__",
            AutoReconnect = false,
            Timeout = 50
        };
        await using var client = new FMdbCommunication(config);

        config.Transport = (MdbTransport)99;

        Exception? exception = await Record.ExceptionAsync(() => client.ConnectAsync());

        Assert.NotNull(exception);
        Assert.IsNotType<ArgumentException>(exception);
        Assert.Equal(Kwy.Communicate.Abstractions.Enums.ConnectionState.Error, client.State);
    }
}
