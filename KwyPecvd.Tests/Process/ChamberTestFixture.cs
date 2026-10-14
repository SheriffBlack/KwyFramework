using KwyPecvd.Device;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;
using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Chamber.Gas;
using KwyPecvd.Process.Chamber.Interlock;
using KwyPecvd.Process.Chamber.Vacuum;

namespace KwyPecvd.Tests.Process;

internal static class ChamberTestFixture
{
    private const string PressureGaugeId =
        "PRESSURE_PM1";

    private const string DryPumpId =
        "PUMP_PM1";

    public static ChamberDefinition
        CreateDefinition(
            HardwareComponentRegistry registry,
            IReadOnlyCollection<
                ChamberGasLineDefinition> gasLines,
            string chamberId = "PM1",
            string displayName =
                "Process Module 1")
    {
        ArgumentNullException.ThrowIfNull(
            registry);

        ArgumentNullException.ThrowIfNull(
            gasLines);

        RegisterReadyVacuumHardware(
            registry,
            chamberId);

        return new ChamberDefinition
        {
            Id = chamberId,
            DisplayName = displayName,
            GasLines = gasLines,
            Vacuum =
                new ChamberVacuumDefinition
                {
                    PressureGaugeId =
                        PressureGaugeId,
                    DryPumpId =
                        DryPumpId
                }
        };
    }

    public static IChamberInterlockSource
        CreateHealthyInterlock()
    {
        return new TestChamberInterlockSource
        {
            Snapshot =
                new ChamberInterlockSnapshot
                {
                    IsValid = true,
                    EmergencyStopHealthy = true,
                    DoorClosed = true,
                    VacuumReady = true,
                    Timestamp =
                        DateTimeOffset.UtcNow
                }
        };
    }

    private static void RegisterReadyVacuumHardware(
        HardwareComponentRegistry registry,
        string moduleId)
    {
        registry.Add(
            new ReadyPressureGauge(
                moduleId));

        registry.Add(
            new ReadyVacuumPump(
                moduleId));
    }

    private sealed class ReadyPressureGauge
        : IPressureGauge
    {
        public ReadyPressureGauge(
            string moduleId)
        {
            Definition =
                new PressureGaugeDefinition
                {
                    Id = PressureGaugeId,
                    ModuleId = moduleId,
                    DisplayName =
                        "Test pressure gauge",
                    Unit = "Pa",
                    Minimum = 0,
                    Maximum = 101_325
                };

            Snapshot =
                new PressureGaugeSnapshot
                {
                    Id = Definition.Id,
                    Pressure = 10,
                    Unit = Definition.Unit,
                    IsOffline = false,
                    IsReadingValid = true,
                    DiagnosticCode = null,
                    Timestamp =
                        DateTimeOffset.UtcNow
                };
        }

        public string Id => Definition.Id;

        public PressureGaugeDefinition
            Definition
        { get; }

        public PressureGaugeSnapshot
            Snapshot
        { get; }

        public event EventHandler<
            PressureGaugeSnapshot>?
            SnapshotChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ExecuteCycleAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ResetAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ShutdownAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;
    }

    private sealed class ReadyVacuumPump
        : IVacuumPump
    {
        public ReadyVacuumPump(
            string moduleId)
        {
            Definition =
                new VacuumPumpDefinition
                {
                    Id = DryPumpId,
                    ModuleId = moduleId,
                    DisplayName =
                        "Test dry pump"
                };

            Snapshot =
                new VacuumPumpSnapshot
                {
                    Id = Definition.Id,
                    CommandedOn = false,
                    IsRunning = false,
                    HasWarning = false,
                    HasAlarm = false,
                    IsOverloaded = false,
                    IsN2Ready = true,
                    IsExhaustReady = true,
                    IsOffline = false,
                    IsFeedbackValid = true,
                    DiagnosticCode = null,
                    Timestamp =
                        DateTimeOffset.UtcNow
                };
        }

        public string Id => Definition.Id;

        public VacuumPumpDefinition
            Definition
        { get; }

        public VacuumPumpSnapshot
            Snapshot
        { get; }

        public event EventHandler<
            VacuumPumpSnapshot>?
            SnapshotChanged
        {
            add { }
            remove { }
        }

        public Task<CommandResult> StartAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.FromResult(
                CommandResult.Success());

        public Task<CommandResult> StopAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.FromResult(
                CommandResult.Success());

        public Task InitializeAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ExecuteCycleAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ResetAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;

        public Task ShutdownAsync(
            CancellationToken cancellationToken =
                default) =>
            Task.CompletedTask;
    }

    private sealed class
        TestChamberInterlockSource
        : IChamberInterlockSource
    {
        public required ChamberInterlockSnapshot
            Snapshot
        { get; init; }
    }
}