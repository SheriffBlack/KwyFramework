using KwyPecvd.Device.VacuumPump;
using KwyPecvd.Device.VacuumPump.PfeifferA100;

namespace KwyPecvd.Tests.Devices;

public sealed class PfeifferA100VacuumPumpTests
{
    [Fact]
    public async Task StartAsync_WritesBrandCommandImmediately()
    {
        var bus = new FakePfeifferA100Bus();
        var pump = CreatePump(bus);

        await pump.InitializeAsync();
        await pump.ExecuteCycleAsync();
        int writesAfterInitialize = bus.WriteCount;

        var result = await pump.StartAsync();

        Assert.True(result.Succeeded);
        Assert.True(bus.LastRunning);
        Assert.Equal(writesAfterInitialize + 1, bus.WriteCount);

        await pump.ExecuteCycleAsync();
        Assert.Equal(writesAfterInitialize + 1, bus.WriteCount);
    }

    private static PfeifferA100VacuumPump CreatePump(IPfeifferA100Bus bus) =>
        new(
            new PfeifferA100Definition
            {
                Pump = new VacuumPumpDefinition
                {
                    Id = "PUMP_PM1_DRY",
                    ModuleId = "PM1",
                    DisplayName = "PM1 Pfeiffer A100"
                },
                Address = "000"
            },
            bus);

    private sealed class FakePfeifferA100Bus : IPfeifferA100Bus
    {
        public bool IsConnected { get; private set; }
        public bool LastRunning { get; private set; }
        public int WriteCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteRunningAsync(
            string address,
            bool running,
            CancellationToken cancellationToken = default)
        {
            LastRunning = running;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task<PfeifferA100Status> ReadStatusAsync(
            string address,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new PfeifferA100Status(
                    IsRunning: LastRunning,
                    HasWarning: false,
                    HasAlarm: false,
                    IsOverTemperature: false));
    }
}
