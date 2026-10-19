using KwyPecvd.Device.Thermal.TemperatureController;
using KwyPecvd.Device.Thermal.TemperatureController.Omron;

namespace KwyPecvd.Tests.Devices;

public sealed class OmronEj1TemperatureControllerTests
{
    [Fact]
    public async Task SetTargetTemperatureAsync_WritesImmediatelyAndCycleDoesNotRewrite()
    {
        var bus = new FakeOmronEj1Bus { RawFeedback = 1_250 };
        var controller = CreateController(bus);

        await controller.InitializeAsync();
        int writesAfterInitialize = bus.WriteCount;

        var result = await controller.SetTargetTemperatureAsync(150);

        Assert.True(result.Succeeded);
        Assert.Equal((ushort)1_500, bus.LastValue);
        Assert.Equal(writesAfterInitialize + 1, bus.WriteCount);

        await controller.ExecuteCycleAsync();
        await controller.ExecuteCycleAsync();

        Assert.Equal(writesAfterInitialize + 1, bus.WriteCount);
        Assert.Equal(125, controller.Snapshot.Feedback);
        Assert.True(controller.Snapshot.IsFeedbackValid);
    }

    [Fact]
    public async Task EnableAsync_WritesChannelRunCommandImmediately()
    {
        var bus = new FakeOmronEj1Bus { RawFeedback = 250 };
        var controller = CreateController(bus);

        await controller.InitializeAsync();
        await controller.ExecuteCycleAsync();

        var result = await controller.EnableAsync();

        Assert.True(result.Succeeded);
        Assert.Equal((ushort)0x0A02, bus.LastValue);
        Assert.Equal((ushort)0xFFFF, bus.LastRegister);
        Assert.True(controller.Snapshot.CommandedEnabled);
    }

    private static OmronEj1TemperatureController CreateController(IOmronEj1Bus bus) =>
        new(
            new OmronEj1Definition
            {
                Controller = new TemperatureControllerDefinition
                {
                    Id = "TC_PM1_WALL",
                    ModuleId = "PM1",
                    DisplayName = "PM1 Wall Heater",
                    Unit = "°C",
                    MinimumSetPoint = 0,
                    MaximumSetPoint = 500,
                    Tolerance = 2
                },
                UnitAddress = 1,
                FeedbackRegister = 0x0600,
                SetPointRegister = 0x0640,
                Channel = 2
            },
            bus);

    private sealed class FakeOmronEj1Bus : IOmronEj1Bus
    {
        public bool IsConnected { get; private set; }
        public short RawFeedback { get; init; }
        public ushort? LastRegister { get; private set; }
        public ushort? LastValue { get; private set; }
        public int WriteCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task<short> ReadHoldingRegisterAsync(
            byte unitAddress,
            ushort register,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RawFeedback);

        public Task WriteSingleRegisterAsync(
            byte unitAddress,
            ushort register,
            ushort value,
            CancellationToken cancellationToken = default)
        {
            LastRegister = register;
            LastValue = value;
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
