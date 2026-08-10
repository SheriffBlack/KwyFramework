namespace KwyTemplate.Flow.DataDeals;

/// <summary>
/// Converts instrument measurements between the engineering units used by station limits and UI.
/// </summary>
public static class MeasurementUnitConverter
{
    public static double Convert(double value, string testName, string? sourceUnit, string? targetUnit)
    {
        if (string.IsNullOrWhiteSpace(targetUnit)
            || UnitsEqual(sourceUnit, targetUnit))
        {
            return value;
        }

        double baseValue = ToBaseUnit(value, testName, sourceUnit);
        return FromBaseUnit(baseValue, testName, targetUnit);
    }

    public static double ToBaseUnit(double value, string testName, string? unit)
        => NormalizeTestName(testName) switch
        {
            "LS" or "LP" => value * InductanceFactor(unit),
            "RS" or "RP" or "Z" or "X" or "DCR" or "DCR1" or "DCR2" => value * ResistanceFactor(unit),
            "CS" or "CP" => value * CapacitanceFactor(unit),
            _ => value
        };

    public static double FromBaseUnit(double value, string testName, string? unit)
        => NormalizeTestName(testName) switch
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

    private static string NormalizeTestName(string value)
        => value.Trim().Replace("θ", "PHASE", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        string value = unit.Trim()
            .Replace("Ω", "Ω", StringComparison.Ordinal)
            .Replace("惟", "Ω", StringComparison.Ordinal)
            .Replace("µ", "μ", StringComparison.Ordinal)
            .Replace("渭", "μ", StringComparison.Ordinal)
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
