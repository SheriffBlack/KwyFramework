using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Instruments.Lcr;

/// <summary>
/// HIOKI LCR meter configuration.
/// </summary>
public class HiokiLcrConfig : IDeviceConfig
{
    private string loadType = HiokiLcrLoadTypes.ZTheta;

    [Browsable(false)]
    public string SupportedModel => "HIOKI_LCR";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("负载类型")]
    [DisplayNameKey("Instrument.Lcr.LoadType")]
    [InputType(InputType.ComboBox)]
    [RefreshPropertyGrid]
    [ItemsSource(
        HiokiLcrLoadTypes.ZTheta,
        HiokiLcrLoadTypes.CsD,
        HiokiLcrLoadTypes.CsRs,
        HiokiLcrLoadTypes.CpD,
        HiokiLcrLoadTypes.CpRp,
        HiokiLcrLoadTypes.LsQ,
        HiokiLcrLoadTypes.LsRs,
        HiokiLcrLoadTypes.LpQ,
        HiokiLcrLoadTypes.LpRp,
        HiokiLcrLoadTypes.RsX)]
    public string LoadType
    {
        get => HiokiLcrLoadTypes.Normalize(loadType);
        set
        {
            loadType = HiokiLcrLoadTypes.Normalize(value);
            CoerceLimitUnits();
        }
    }

    [Category("主测试项")]
    [CategorySource(nameof(Parameter1DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter1UnitItems))]
    public double Parameter1Max { get; set; } = 1000.0;

    [Browsable(false)]
    public string Parameter1MaxUnit { get; set; } = "Ω";

    [Category("主测试项")]
    [CategorySource(nameof(Parameter1DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter1UnitItems))]
    public double Parameter1Min { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter1MinUnit { get; set; } = "Ω";

    [Category("副测试项")]
    [CategorySource(nameof(Parameter3DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter3UnitItems))]
    public double Parameter3Max { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter3MaxUnit { get; set; } = "°";

    [Category("副测试项")]
    [CategorySource(nameof(Parameter3DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter3UnitItems))]
    public double Parameter3Min { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter3MinUnit { get; set; } = "°";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试频率")]
    [DisplayNameKey("Instrument.Lcr.Frequency")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    public double Frequency { get; set; } = 1000.0;

    [Browsable(false)]
    public string FrequencyUnit { get; set; } = "Hz";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试电压")]
    [DisplayNameKey("Instrument.Lcr.Voltage")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("mV", "V")]
    public double Voltage { get; set; } = 1.0;

    [Browsable(false)]
    public string VoltageUnit { get; set; } = "V";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试延迟 (s)")]
    [DisplayNameKey("Instrument.Lcr.Delay")]
    [InputType(InputType.TextBox)]
    public double Delay { get; set; } = 0.0;

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("量程")]
    [DisplayNameKey("Instrument.Lcr.Range")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("100mΩ", "1Ω", "10Ω", "300Ω", "1KΩ", "3KΩ", "10KΩ", "30KΩ", "100KΩ", "1MΩ", "10MΩ", "100MΩ")]
    public string Range { get; set; } = "100mΩ";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测量速度")]
    [DisplayNameKey("Instrument.Lcr.Speed")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("FAST", "MED", "SLOW", "SLOW2")]
    public string Speed { get; set; } = "MED";

    [Browsable(false)]
    public string Parameter1DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetActiveParameterPair().Parameter1);

    [Browsable(false)]
    public string Parameter3DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetActiveParameterPair().Parameter3);

    [Browsable(false)]
    public IReadOnlyList<string> Parameter1UnitItems => HiokiLcrParameterUnits.GetUnits(GetActiveParameterPair().Parameter1);

    [Browsable(false)]
    public IReadOnlyList<string> Parameter3UnitItems => HiokiLcrParameterUnits.GetUnits(GetActiveParameterPair().Parameter3);

    private void CoerceLimitUnits()
    {
        Parameter1MaxUnit = CoerceUnit(Parameter1MaxUnit, Parameter1UnitItems);
        Parameter1MinUnit = CoerceUnit(Parameter1MinUnit, Parameter1UnitItems);
        Parameter3MaxUnit = CoerceUnit(Parameter3MaxUnit, Parameter3UnitItems);
        Parameter3MinUnit = CoerceUnit(Parameter3MinUnit, Parameter3UnitItems);
    }

    private static string CoerceUnit(string? unit, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return string.Empty;
        }

        return candidates.Any(candidate => string.Equals(candidate, unit, StringComparison.OrdinalIgnoreCase))
            ? unit ?? string.Empty
            : candidates[0];
    }

    public HiokiLcrParameterPair GetActiveParameterPair()
        => HiokiLcrLoadTypes.Resolve(LoadType);

    public bool Validate() => true;
}

public sealed record HiokiLcrParameterPair(string Parameter1, string Parameter3);

public static class HiokiLcrParameterUnits
{
    private static readonly string[] ResistanceUnits = ["Ω", "mΩ", "μΩ"];
    private static readonly string[] InductanceUnits = ["H", "mH", "μH", "nH"];
    private static readonly string[] CapacitanceUnits = ["F", "mF", "μF", "nF", "pF"];
    private static readonly string[] PhaseUnits = ["°"];
    private static readonly string[] DimensionlessUnits = [""];

    public static IReadOnlyList<string> GetUnits(string parameter)
        => NormalizeParameter(parameter) switch
        {
            "Z" or "RS" or "RP" or "X" => ResistanceUnits,
            "LS" or "LP" => InductanceUnits,
            "CS" or "CP" => CapacitanceUnits,
            "PHAS" or "PHASE" => PhaseUnits,
            "D" or "Q" => DimensionlessUnits,
            _ => DimensionlessUnits
        };

    private static string NormalizeParameter(string? parameter)
        => string.IsNullOrWhiteSpace(parameter)
            ? string.Empty
            : parameter.Trim()
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
}

public static class HiokiLcrLoadTypes
{
    public const string ZTheta = "Z-\u03b8";
    public const string CsD = "Cs-D";
    public const string CsRs = "Cs-Rs";
    public const string CpD = "Cp-D";
    public const string CpRp = "Cp-Rp";
    public const string LsQ = "Ls-Q";
    public const string LsRs = "Ls-Rs";
    public const string LpQ = "Lp-Q";
    public const string LpRp = "Lp-Rp";
    public const string RsX = "Rs-X";

    public static IReadOnlyList<string> All { get; } =
[
    ZTheta,
        CsD,
        CsRs,
        CpD,
        CpRp,
        LsQ,
        LsRs,
        LpQ,
        LpRp,
        RsX
];

    public static string Normalize(string? loadType)
    {
        string key = NormalizeKey(loadType);
        return key switch
        {
            "ZTHETA" or "ZPHAS" or "ZPHASE" => ZTheta,
            "CSD" => CsD,
            "CSRS" => CsRs,
            "CPD" => CpD,
            "CPRP" => CpRp,
            "LSQ" => LsQ,
            "LSRS" => LsRs,
            "LPQ" => LpQ,
            "LPRP" => LpRp,
            "RSX" => RsX,
            _ => ZTheta
        };
    }

    public static int GetModeNo(string? loadType)
        => Normalize(loadType) switch
        {
            ZTheta => 1,
            CsD => 2,
            CsRs => 3,
            CpD => 4,
            CpRp => 5,
            LsQ => 6,
            LsRs => 7,
            LpQ => 8,
            LpRp => 9,
            RsX => 10,
            _ => 1
        };

    public static HiokiLcrParameterPair Resolve(string? loadType)
        => Normalize(loadType) switch
        {
            CsD => new HiokiLcrParameterPair("C_S", "D"),
            CsRs => new HiokiLcrParameterPair("C_S", "R_S"),
            CpD => new HiokiLcrParameterPair("C_P", "D"),
            CpRp => new HiokiLcrParameterPair("C_P", "R_P"),
            LsQ => new HiokiLcrParameterPair("L_S", "Q"),
            LsRs => new HiokiLcrParameterPair("L_S", "R_S"),
            LpQ => new HiokiLcrParameterPair("L_P", "Q"),
            LpRp => new HiokiLcrParameterPair("L_P", "R_P"),
            RsX => new HiokiLcrParameterPair("R_S", "X"),
            _ => new HiokiLcrParameterPair("Z", "PHAS")
        };

    public static string ToDisplayParameter(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return string.Empty;
        }

        return parameter.Trim().ToUpperInvariant() switch
        {
            "OFF" => string.Empty,
            "L_S" or "LS" => "Ls",
            "L_P" or "LP" => "Lp",
            "R_S" or "RS" => "Rs",
            "R_P" or "RP" => "Rp",
            "C_S" or "CS" => "Cs",
            "C_P" or "CP" => "Cp",
            "PHAS" or "PHASE" => "PHASE",
            _ => parameter.Trim().ToUpperInvariant()
        };
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u03b8", "THETA", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();
    }
}









