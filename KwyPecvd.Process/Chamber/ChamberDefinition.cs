using KwyPecvd.Process.Chamber.Gas;
using KwyPecvd.Process.Chamber.Thermal;
using KwyPecvd.Process.Chamber.Vacuum;

namespace KwyPecvd.Process.Chamber;

/*
 
 ChamberDefinition
├─ GasLines[]
├─ Vacuum
└─ Thermal
   └─ Zones[]
      ├─ ControllerId
      └─ MonitoringSensorIds[]
*/

/// <summary>
/// 一套PECVD工艺腔室的静态组成。
/// </summary>
public sealed record ChamberDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public IReadOnlyCollection<ChamberGasLineDefinition> GasLines { get; init; } = Array.Empty<ChamberGasLineDefinition>();

    public required ChamberVacuumDefinition Vacuum { get; init; }

    public required ChamberThermalDefinition Thermal { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        ArgumentNullException.ThrowIfNull(Vacuum);
        ArgumentNullException.ThrowIfNull(Thermal);

        Vacuum.Validate();
        Thermal.Validate();

        foreach (var gasLine in GasLines)
        {
            gasLine.Validate();
        }

        var duplicateGas = GasLines
            .GroupBy(
                gasLine => gasLine.GasName,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateGas is not null)
        {
            throw new InvalidOperationException(
                $"Chamber {Id} contains duplicate gas " +
                $"'{duplicateGas.Key}'.");
        }
    }
}