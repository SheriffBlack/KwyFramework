using KwyPecvd.Device;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;
using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Chamber.Gas;
using KwyPecvd.Process.Chamber.Interlock;
using KwyPecvd.Process.Chamber.Vacuum;
using KwyPecvd.Device.OnOffValve;
using KwyPecvd.Device.Thermal.TemperatureController;
using KwyPecvd.Process.Chamber.Thermal;

namespace KwyPecvd.Tests.Process;

internal static class ChamberTestFixture
{
    private const string IsolationValveId = "VALVE_PM1_ISOLATION";
    private const string PressureGaugeId = "PRESSURE_PM1";

    private const string DryPumpId = "PUMP_PM1";
    private const string TemperatureControllerId = "TC_PM1_WALL";

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

        registry.Add(
            new ReadyTemperatureController(
                chamberId));

        return new ChamberDefinition
        {
            Id = chamberId,
            DisplayName = displayName,
            GasLines = gasLines,
            Vacuum = new ChamberVacuumDefinition
            {
                PressureGauges =
                [
                    new ChamberPressureGaugeDefinition
                    {
                        PressureGaugeId =
                            PressureGaugeId,
                        Roles =
                            ChamberPressureGaugeRole.PumpDown |
                            ChamberPressureGaugeRole.ProcessControl |
                            ChamberPressureGaugeRole.Safety
                    }
                ],

                PumpIds =
                [
                    DryPumpId
                ],

                ValveIds =
                [
                    IsolationValveId
                ],

                ReadyPressure = 100,

                PumpDownTimeout =
                    TimeSpan.FromMinutes(5)
            },
            Thermal = new ChamberThermalDefinition
            {
                Zones =
                [
                    new ChamberThermalZoneDefinition
                    {
                        ZoneId = "Wall",
                        DisplayName = "Chamber wall",
                        ControllerId = TemperatureControllerId
                    }
                ],
                StableDuration = TimeSpan.Zero,
                HeatingTimeout = TimeSpan.FromMinutes(30)
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

        registry.Add(
            new ReadyIsolationValve(
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

    private sealed class ReadyIsolationValve
    : IOnOffValve
    {
        public ReadyIsolationValve(
            string moduleId)
        {
            Definition =
                new OnOffValveDefinition
                {
                    Id = IsolationValveId,
                    ModuleId = moduleId,
                    DisplayName =
                        "Test isolation valve",
                    SafePosition =
                        OnOffValvePosition.Closed,
                    MotionTimeout =
                        TimeSpan.FromSeconds(5)
                };

            Snapshot =
                new OnOffValveSnapshot
                {
                    Id = Definition.Id,
                    CommandedPosition =
                        OnOffValvePosition.Closed,
                    State =
                        OnOffValveState.Closed,
                    IsOffline = false,
                    IsFeedbackValid = true,
                    IsMotionTimedOut = false,
                    DiagnosticCode = null,
                    Timestamp =
                        DateTimeOffset.UtcNow
                };
        }

        public string Id => Definition.Id;

        public OnOffValveDefinition Definition
        {
            get;
        }

        public OnOffValveSnapshot Snapshot
        {
            get;
            private set;
        }

        public event EventHandler<OnOffValveSnapshot>?
            SnapshotChanged
        {
            add { }
            remove { }
        }

        public Task<CommandResult> OpenAsync(
            CancellationToken cancellationToken = default)
        {
            Snapshot = Snapshot with
            {
                CommandedPosition =
                    OnOffValvePosition.Open,
                State =
                    OnOffValveState.Open,
                Timestamp =
                    DateTimeOffset.UtcNow
            };

            return Task.FromResult(
                CommandResult.Success());
        }

        public Task<CommandResult> CloseAsync(
            CancellationToken cancellationToken = default)
        {
            Snapshot = Snapshot with
            {
                CommandedPosition =
                    OnOffValvePosition.Closed,
                State =
                    OnOffValveState.Closed,
                Timestamp =
                    DateTimeOffset.UtcNow
            };

            return Task.FromResult(
                CommandResult.Success());
        }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExecuteCycleAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ShutdownAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ReadyTemperatureController
        : ITemperatureController
    {
        public ReadyTemperatureController(string moduleId)
        {
            Definition = new TemperatureControllerDefinition
            {
                Id = TemperatureControllerId,
                ModuleId = moduleId,
                DisplayName = "Test wall heater",
                Unit = "°C",
                MinimumSetPoint = 0,
                MaximumSetPoint = 500,
                Tolerance = 2,
                ToleranceDelay = TimeSpan.Zero
            };

            Snapshot = new TemperatureControllerSnapshot
            {
                Id = Definition.Id,
                SetPoint = 25,
                Feedback = 25,
                Unit = Definition.Unit,
                CommandedEnabled = false,
                IsPowerOn = false,
                IsOffline = false,
                IsFeedbackValid = true,
                IsOutOfTolerance = false,
                DiagnosticCode = null,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        public string Id => Definition.Id;
        public TemperatureControllerDefinition Definition { get; }
        public TemperatureControllerSnapshot Snapshot { get; private set; }

        public event EventHandler<TemperatureControllerSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }

        public Task<CommandResult> SetTargetTemperatureAsync(
            double target,
            CancellationToken cancellationToken = default)
        {
            Snapshot = Snapshot with
            {
                SetPoint = target,
                Feedback = target,
                Timestamp = DateTimeOffset.UtcNow
            };
            return Task.FromResult(CommandResult.Success());
        }

        public Task<CommandResult> EnableAsync(
            CancellationToken cancellationToken = default)
        {
            Snapshot = Snapshot with
            {
                CommandedEnabled = true,
                IsPowerOn = true,
                Timestamp = DateTimeOffset.UtcNow
            };
            return Task.FromResult(CommandResult.Success());
        }

        public Task<CommandResult> DisableAsync(
            CancellationToken cancellationToken = default)
        {
            Snapshot = Snapshot with
            {
                CommandedEnabled = false,
                IsPowerOn = false,
                Timestamp = DateTimeOffset.UtcNow
            };
            return Task.FromResult(CommandResult.Success());
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExecuteCycleAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ShutdownAsync(CancellationToken cancellationToken = default) =>
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
