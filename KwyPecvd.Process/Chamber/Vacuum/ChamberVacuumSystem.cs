using KwyPecvd.Device;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 负责腔体真空硬件绑定和状态聚合。
/// </summary>
internal sealed class ChamberVacuumSystem
{
    private readonly string chamberId;

    private readonly
        ChamberVacuumDefinition definition;

    private readonly IHardwareComponentRegistry
        registry;

    public ChamberVacuumSystem(
        string chamberId,
        ChamberVacuumDefinition definition,
        IHardwareComponentRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            chamberId);

        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            registry);

        definition.Validate();

        this.chamberId = chamberId;
        this.definition = definition;
        this.registry = registry;
    }

    public ChamberVacuumSnapshot CreateSnapshot()
    {
        var pressureGauge =
            GetRequiredPressureGauge();

        var dryPump =
            GetRequiredDryPump();

        return new ChamberVacuumSnapshot
        {
            PressureGauge =
                pressureGauge.Snapshot,

            DryPump =
                dryPump.Snapshot
        };
    }

    public bool TryValidateReady(
        out CommandResult failure)
    {
        IPressureGauge pressureGauge;

        try
        {
            pressureGauge =
                GetRequiredPressureGauge();
        }
        catch (Exception exception)
        {
            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                exception.Message);

            return false;
        }

        var snapshot = pressureGauge.Snapshot;

        if (snapshot.IsOffline)
        {
            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                $"Pressure gauge " +
                $"'{pressureGauge.Id}' is offline.");

            return false;
        }

        if (!snapshot.IsReadingValid)
        {
            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                $"Pressure gauge " +
                $"'{pressureGauge.Id}' reading " +
                $"is invalid. Diagnostic: " +
                $"{snapshot.DiagnosticCode ??
                   "UNKNOWN"}.");

            return false;
        }

        failure = CommandResult.Success();
        return true;
    }

    private IPressureGauge GetRequiredPressureGauge()
    {
        var pressureGauge =
            registry.GetRequired<IPressureGauge>(
                definition.PressureGaugeId);

        if (!string.Equals(
                pressureGauge.Definition.ModuleId,
                chamberId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Pressure gauge " +
                $"'{pressureGauge.Id}' belongs to " +
                $"module " +
                $"'{pressureGauge.Definition.ModuleId}', " +
                $"not chamber '{chamberId}'.");
        }

        return pressureGauge;
    }

    private IVacuumPump GetRequiredDryPump()
    {
        var pump =
            registry.GetRequired<IVacuumPump>(
                definition.DryPumpId);

        if (!string.Equals(
                pump.Definition.ModuleId,
                chamberId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Vacuum pump '{pump.Id}' belongs " +
                $"to module " +
                $"'{pump.Definition.ModuleId}', " +
                $"not chamber '{chamberId}'.");
        }

        return pump;
    }
}