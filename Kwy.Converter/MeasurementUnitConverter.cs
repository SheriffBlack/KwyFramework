namespace Kwy.Converter;

/// <summary>
/// Converts engineering measurement values between their display units and SI base units.
/// This class is intentionally independent of UI, flow and device protocols.
/// </summary>
public static class MeasurementUnitConverter
{
    public static double Convert(double value, string quantity, string? sourceUnit, string? targetUnit)
    {
        if (string.IsNullOrWhiteSpace(targetUnit) || UnitsEqual(sourceUnit, targetUnit))
        {
            return value;
        }

        return FromBaseUnit(ToBaseUnit(value, quantity, sourceUnit), quantity, targetUnit);
    }

    public static double ToBaseUnit(double value, string quantity, string? unit)
        => NormalizeQuantity(quantity) switch
        {
            "LS" or "LP" => value * InductanceFactor(unit),
            "RS" or "RP" or "Z" or "X" or "DCR" or "DCR1" or "DCR2" => value * ResistanceFactor(unit),
            "CS" or "CP" => value * CapacitanceFactor(unit),
            _ => value
        };

    public static double FromBaseUnit(double value, string quantity, string? unit)
        => NormalizeQuantity(quantity) switch
        {
            "LS" or "LP" => value / InductanceFactor(unit),
            "RS" or "RP" or "Z" or "X" or "DCR" or "DCR1" or "DCR2" => value / ResistanceFactor(unit),
            "CS" or "CP" => value / CapacitanceFactor(unit),
            _ => value
        };

    private static bool UnitsEqual(string? left, string? right)
        => string.Equals(NormalizeUnit(left), NormalizeUnit(right), StringComparison.Ordinal);

    private static double ResistanceFactor(string? unit)
        => NormalizeUnit(unit) switch
        {
            "UOHM" => 1e-6,
            "MOHM" => 1e-3,
            "KOHM" => 1e3,
            "MEGOHM" => 1e6,
            _ => 1d
        };

    private static double InductanceFactor(string? unit)
        => NormalizeUnit(unit) switch
        {
            "NH" => 1e-9,
            "UH" => 1e-6,
            "MH" => 1e-3,
            _ => 1d
        };

    private static double CapacitanceFactor(string? unit)
        => NormalizeUnit(unit) switch
        {
            "PF" => 1e-12,
            "NF" => 1e-9,
            "UF" => 1e-6,
            "MF" => 1e-3,
            _ => 1d
        };

    private static string NormalizeQuantity(string value)
        => value.Trim().Replace("θ", "PHASE", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        string value = unit.Trim()
            .Replace("Ω", "Ω", StringComparison.Ordinal)
            .Replace("\u60DF", "Ω", StringComparison.Ordinal)
            .Replace("µ", "μ", StringComparison.Ordinal)
            .Replace("\u6E2D", "μ", StringComparison.Ordinal)
            .Replace("u", "μ", StringComparison.OrdinalIgnoreCase);

        return value switch
        {
            "Ω" or "ohm" or "OHM" => "OHM",
            "mΩ" or "mohm" or "MOHM" => "MOHM",
            "μΩ" or "μohm" or "UOHM" => "UOHM",
            "kΩ" or "kohm" or "KOHM" => "KOHM",
            "MΩ" or "Mohm" or "MEGOHM" => "MEGOHM",
            "H" or "h" => "H",
            "mH" => "MH",
            "μH" => "UH",
            "nH" => "NH",
            "F" or "f" => "F",
            "mF" => "MF",
            "μF" => "UF",
            "nF" => "NF",
            "pF" => "PF",
            _ => value.ToUpperInvariant()
        };
    }
}
