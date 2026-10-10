using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KwyPecvd.Process.Chambers;

/// <summary>
/// 一套PECVD工艺腔室的静态组成。
/// </summary>
public sealed record ChamberDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public IReadOnlyCollection<ChamberGasLineDefinition> GasLines { get; init; } = Array.Empty<ChamberGasLineDefinition>();

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            DisplayName);

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