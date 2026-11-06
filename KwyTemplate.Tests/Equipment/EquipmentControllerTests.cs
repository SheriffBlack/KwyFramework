using KwyTemplate.Equipment;
using KwyTemplate.Process.Machines;
using KwyTemplate.Process.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KwyTemplate.Tests.Equipment;

public sealed class EquipmentControllerTests
{
    [Fact]
    public async Task StartAndStop_AreAuthorizedAndDelegatedToProcess()
    {
        var process = new TestMachine();
        IEquipmentController controller = CreateController(process);

        EquipmentCommandResult start = await controller.StartProductionAsync();

        Assert.True(start.Succeeded);
        Assert.Equal(EquipmentState.Running, controller.State);
        Assert.Equal(1, process.StartCount);
        Assert.False(controller.Evaluate(EquipmentCommand.StartProduction).IsAllowed);

        EquipmentCommandResult stop = await controller.StopProductionAsync();

        Assert.True(stop.Succeeded);
        Assert.Equal(EquipmentState.Stopped, controller.State);
        Assert.Equal(1, process.StopCount);
    }

    [Fact]
    public async Task Stop_WhenStopped_IsDeniedWithoutCallingProcess()
    {
        var process = new TestMachine();
        IEquipmentController controller = CreateController(process);

        EquipmentCommandResult result = await controller.StopProductionAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Equipment.NotRunning", result.ReasonCode);
        Assert.Equal(0, process.StopCount);
    }

    private static IEquipmentController CreateController(IMachine process)
    {
        var services = new ServiceCollection();
        services.AddSingleton(process);
        new EquipmentModule().RegisterTypes(services);
        return services.BuildServiceProvider().GetRequiredService<IEquipmentController>();
    }

    private sealed class TestMachine : IMachine
    {
        public event EventHandler? RunningStateChanged;

        public string MachineId => "Test";
        public string MachineName => "Test";
        public bool IsRunning { get; private set; }
        public MachineProductionState ProductionState => IsRunning
            ? MachineProductionState.Running
            : MachineProductionState.Stopped;
        public IReadOnlyList<TestStationModel> Stations { get; } = [];
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            IsRunning = true;
            RunningStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCount++;
            IsRunning = false;
            RunningStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteStationAsync(
            int stationId,
            bool triggerResult = true,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
