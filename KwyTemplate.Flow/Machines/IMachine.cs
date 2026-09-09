using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.Machines;

public interface IMachine : IDisposable
{
    string MachineId { get; }

    string MachineName { get; }

    bool IsRunning { get; }

    MachineProductionState ProductionState { get; }

    IReadOnlyList<TestStationModel> Stations { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task ExecuteStationAsync(int stationId, bool triggerResult = true, CancellationToken cancellationToken = default);
}

public interface IMachineResultProvider
{
    event EventHandler? ResultTableChanged;

    IReadOnlyList<MachineResultColumn> ResultColumns { get; }

    IReadOnlyList<MachineResultRow> ResultRows { get; }
}

public interface IStationOperationMachine
{
    IReadOnlyList<StationOperationDescriptor> GetStationOperations(TestStationModel station);

    Task<bool> ExecuteStationOperationAsync(
        TestStationModel station,
        string operationCode,
        CancellationToken cancellationToken = default);
}

