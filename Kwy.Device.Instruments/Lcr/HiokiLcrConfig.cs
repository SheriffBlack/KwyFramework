using System.ComponentModel;
using System.Text.Json.Serialization;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Instruments.Lcr;

/// <summary>
/// HIOKI LCR meter configuration.
/// </summary>
public class HiokiLcrConfig : IDeviceConfig
{
    public const string SingleFrequency = "单频";
    public const string DualFrequency = "双频";
    public const string OffOff = "OFF-OFF";
    private string loadType = HiokiLcrLoadTypes.ZTheta;
    private string secondLoadType = HiokiLcrLoadTypes.LsRs;

    /// <summary>
    /// Runtime capability supplied by the device definition.  It is deliberately
    /// not a recipe field, so a shared configuration model can still be used by
    /// HIOKI 3533/3536 without exposing unsupported editors.
    /// </summary>
    [Browsable(false)]
    [JsonIgnore]
    public bool SupportsDualFrequency { get; set; }

    [Browsable(false)]
    [JsonIgnore]
    public bool SecondFrequencyQEnabled { get; set; }

    [Browsable(false)]
    [JsonIgnore]
    public bool IsDualFrequencyEnabled => SupportsDualFrequency && string.Equals(FrequencyMode, DualFrequency, StringComparison.Ordinal);

    [Browsable(false)]
    [JsonIgnore]
    public bool IsSingleFrequencyEnabled => !IsDualFrequencyEnabled;

    [Browsable(false)]
    [JsonIgnore]
    public bool IsMeasurementDisabled => GetActiveParameterPair() is { Parameter1: "OFF", Parameter3: "OFF" };

    [Browsable(false)]
    [JsonIgnore]
    public bool IsSingleFrequencyMeasurementEnabled => IsSingleFrequencyEnabled && !IsMeasurementDisabled;

    [Browsable(false)]
    [JsonIgnore]
    public bool IsDualFrequencyMeasurementEnabled => IsDualFrequencyEnabled && !IsMeasurementDisabled;

    [Browsable(false)]
    [JsonIgnore]
    public bool IsSecondLoadTypeLocked => SecondFrequencyQEnabled;

    [Browsable(false)]
    public string SupportedModel => "HIOKI_LCR";

    // Keep this ungrouped and declared before all instrument parameters, so
    // the property grid renders the mode selector as the first compact row.
    [VisibleWhen(nameof(SupportsDualFrequency))]
    [DisplayName("频率模式")]
    [InputType(InputType.RadioButton)]
    [ItemsSource(SingleFrequency, DualFrequency)]
    [GroupWidth(0.5)]
    [InlineGroup("FrequencyMode")]
    [RefreshPropertyGrid]
    public string FrequencyMode { get; set; } = SingleFrequency;

    [VisibleWhen(nameof(IsSingleFrequencyEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("负载类型")]
    [DisplayNameKey("Instrument.Lcr.LoadType")]
    [InputType(InputType.ComboBox)]
    [RefreshPropertyGrid]
    [ItemsSource(
        HiokiLcrLoadTypes.OffOff,
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

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("主测试项")]
    [CategorySource(nameof(Parameter1DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter1UnitItems))]
    public double Parameter1UpperLimit { get; set; } = 1000.0;

    [Browsable(false)]
    public string Parameter1UpperLimitUnit { get; set; } = "Ω";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("主测试项")]
    [CategorySource(nameof(Parameter1DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter1UnitItems))]
    public double Parameter1LowerLimit { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter1LowerLimitUnit { get; set; } = "Ω";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("副测试项")]
    [CategorySource(nameof(Parameter3DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter3UnitItems))]
    public double Parameter3UpperLimit { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter3UpperLimitUnit { get; set; } = "°";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("副测试项")]
    [CategorySource(nameof(Parameter3DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(Parameter3UnitItems))]
    public double Parameter3LowerLimit { get; set; } = 0.0;

    [Browsable(false)]
    public string Parameter3LowerLimitUnit { get; set; } = "°";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试频率")]
    [DisplayNameKey("Instrument.Lcr.Frequency")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    public double Frequency { get; set; } = 1000.0;

    [Browsable(false)]
    public string FrequencyUnit { get; set; } = "Hz";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试电压")]
    [DisplayNameKey("Instrument.Lcr.Voltage")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("mV", "V")]
    public double Voltage { get; set; } = 1.0;

    [Browsable(false)]
    public string VoltageUnit { get; set; } = "V";

    [VisibleWhen(nameof(IsSingleFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试延迟 (s)")]
    [DisplayNameKey("Instrument.Lcr.Delay")]
    [InputType(InputType.TextBox)]
    public double Delay { get; set; } = 0.0;

    [VisibleWhen(nameof(IsSingleFrequencyEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("量程")]
    [DisplayNameKey("Instrument.Lcr.Range")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("100mΩ", "1Ω", "10Ω", "300Ω", "1KΩ", "3KΩ", "10KΩ", "30KΩ", "100KΩ", "1MΩ", "10MΩ", "100MΩ")]
    public string Range { get; set; } = "100mΩ";

    [VisibleWhen(nameof(IsSingleFrequencyEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测量速度")]
    [DisplayNameKey("Instrument.Lcr.Speed")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("FAST", "MED", "SLOW", "SLOW2")]
    public string Speed { get; set; } = "MED";

    [VisibleWhen(nameof(IsDualFrequencyEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("负载类型")]
    [DisplayNameKey("Instrument.Lcr.LoadType")]
    [InputType(InputType.ComboBox)]
    [ItemsSource(HiokiLcrLoadTypes.OffOff, HiokiLcrLoadTypes.LsRs, HiokiLcrLoadTypes.LsQ)]
    [DisableWhen(nameof(IsSecondLoadTypeLocked))]
    [RefreshPropertyGrid]
    public string SecondLoadType
    {
        get => SecondFrequencyQEnabled ? HiokiLcrLoadTypes.LsQ : HiokiLcrLoadTypes.Normalize(secondLoadType);
        set
        {
            secondLoadType = HiokiLcrLoadTypes.Normalize(value);
            CoerceSecondLimitUnits();
        }
    }

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试频率")]
    [DisplayNameKey("Instrument.Lcr.Frequency")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    public double Frequency2 { get; set; } = 1000.0;

    [Browsable(false)]
    public string Frequency2Unit { get; set; } = "Hz";

    // The remaining conditions are shared with frequency 1.  They are kept in
    // the same layout so operators can verify them, while the read-only state
    // makes it explicit that frequency 2 cannot change them independently.
    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试电压")]
    [DisplayNameKey("Instrument.Lcr.Voltage")]
    [ReadOnly(true)]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("mV", "V")]
    public double SecondVoltage => Voltage;

    [Browsable(false)]
    public string SecondVoltageUnit => VoltageUnit;

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试延迟 (s)")]
    [DisplayNameKey("Instrument.Lcr.Delay")]
    [ReadOnly(true)]
    [InputType(InputType.TextBox)]
    public double SecondDelay => Delay;

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("量程")]
    [DisplayNameKey("Instrument.Lcr.Range")]
    [ReadOnly(true)]
    [InputType(InputType.RadioButton)]
    [ItemsSource("100mΩ", "1Ω", "10Ω", "300Ω", "1KΩ", "3KΩ", "10KΩ", "30KΩ", "100KΩ", "1MΩ", "10MΩ", "100MΩ")]
    public string SecondRange => Range;

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测量速度")]
    [DisplayNameKey("Instrument.Lcr.Speed")]
    [ReadOnly(true)]
    [InputType(InputType.RadioButton)]
    [ItemsSource("FAST", "MED", "SLOW", "SLOW2")]
    public string SecondSpeed => Speed;

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("主测试项")]
    [CategorySource(nameof(SecondParameter1DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(SecondParameter1UnitItems))]
    public double SecondParameter1UpperLimit { get; set; }

    [Browsable(false)]
    public string SecondParameter1UpperLimitUnit { get; set; } = "μH";

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("主测试项")]
    [CategorySource(nameof(SecondParameter1DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(SecondParameter1UnitItems))]
    public double SecondParameter1LowerLimit { get; set; }

    [Browsable(false)]
    public string SecondParameter1LowerLimitUnit { get; set; } = "μH";

    [VisibleWhen(nameof(IsDualFrequencyMeasurementEnabled))]
    [Category("副测试项")]
    [CategorySource(nameof(SecondParameter3DisplayName))]
    [DisplayName("上限")]
    [DisplayNameKey("Instrument.Limit.Upper")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(SecondParameter3UnitItems))]
    public double SecondParameter3UpperLimit { get; set; }

    [Browsable(false)]
    public string SecondParameter3UpperLimitUnit { get; set; } = "mΩ";

    [VisibleWhen(nameof(IsDualFrequencyEnabled))]
    [Category("副测试项")]
    [CategorySource(nameof(SecondParameter3DisplayName))]
    [DisplayName("下限")]
    [DisplayNameKey("Instrument.Limit.Lower")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSourceProvider(nameof(SecondParameter3UnitItems))]
    public double SecondParameter3LowerLimit { get; set; }

    [Browsable(false)]
    public string SecondParameter3LowerLimitUnit { get; set; } = "mΩ";

    [Browsable(false)]
    public string Parameter1DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetActiveParameterPair().Parameter1);

    [Browsable(false)]
    public string Parameter3DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetActiveParameterPair().Parameter3);

    [Browsable(false)]
    public string SecondParameter1DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetSecondParameterPair().Parameter1);

    [Browsable(false)]
    public string SecondParameter3DisplayName => HiokiLcrLoadTypes.ToDisplayParameter(GetSecondParameterPair().Parameter3);

    [Browsable(false)]
    public IReadOnlyList<string> Parameter1UnitItems => HiokiLcrParameterUnits.GetUnits(GetActiveParameterPair().Parameter1);

    [Browsable(false)]
    public IReadOnlyList<string> Parameter3UnitItems => HiokiLcrParameterUnits.GetUnits(GetActiveParameterPair().Parameter3);

    [Browsable(false)]
    public IReadOnlyList<string> SecondParameter1UnitItems => HiokiLcrParameterUnits.GetUnits(GetSecondParameterPair().Parameter1);

    [Browsable(false)]
    public IReadOnlyList<string> SecondParameter3UnitItems => HiokiLcrParameterUnits.GetUnits(GetSecondParameterPair().Parameter3);

    private void CoerceLimitUnits()
    {
        Parameter1UpperLimitUnit = CoerceUnit(Parameter1UpperLimitUnit, Parameter1UnitItems);
        Parameter1LowerLimitUnit = CoerceUnit(Parameter1LowerLimitUnit, Parameter1UnitItems);
        Parameter3UpperLimitUnit = CoerceUnit(Parameter3UpperLimitUnit, Parameter3UnitItems);
        Parameter3LowerLimitUnit = CoerceUnit(Parameter3LowerLimitUnit, Parameter3UnitItems);
    }

    private void CoerceSecondLimitUnits()
    {
        SecondParameter1UpperLimitUnit = CoerceUnit(SecondParameter1UpperLimitUnit, SecondParameter1UnitItems);
        SecondParameter1LowerLimitUnit = CoerceUnit(SecondParameter1LowerLimitUnit, SecondParameter1UnitItems);
        SecondParameter3UpperLimitUnit = CoerceUnit(SecondParameter3UpperLimitUnit, SecondParameter3UnitItems);
        SecondParameter3LowerLimitUnit = CoerceUnit(SecondParameter3LowerLimitUnit, SecondParameter3UnitItems);
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
        => GetActiveMeasurementSettings().Parameters;

    public HiokiLcrParameterPair GetSecondParameterPair()
        => HiokiLcrLoadTypes.Resolve(SecondLoadType);

    /// <summary>
    /// Returns the one parameter group selected by the current frequency mode.
    /// Protocol writing, display conversion and software limits must all use
    /// this value rather than branching independently on <see cref="FrequencyMode"/>.
    /// </summary>
    public HiokiLcrMeasurementSettings GetActiveMeasurementSettings()
        => IsDualFrequencyEnabled
            ? new(
                GetSecondParameterPair(),
                Frequency2,
                Frequency2Unit,
                new(SecondParameter1LowerLimit, SecondParameter1LowerLimitUnit, SecondParameter1UpperLimit, SecondParameter1UpperLimitUnit),
                new(SecondParameter3LowerLimit, SecondParameter3LowerLimitUnit, SecondParameter3UpperLimit, SecondParameter3UpperLimitUnit))
            : new(
                HiokiLcrLoadTypes.Resolve(LoadType),
                Frequency,
                FrequencyUnit,
                new(Parameter1LowerLimit, Parameter1LowerLimitUnit, Parameter1UpperLimit, Parameter1UpperLimitUnit),
                new(Parameter3LowerLimit, Parameter3LowerLimitUnit, Parameter3UpperLimit, Parameter3UpperLimitUnit));

    public bool Validate() => true;
}

public sealed record HiokiLcrParameterPair(string Parameter1, string Parameter3);

public sealed record HiokiLcrMeasurementLimit(double Minimum, string MinimumUnit, double Maximum, string MaximumUnit);

public sealed record HiokiLcrMeasurementSettings(
    HiokiLcrParameterPair Parameters,
    double Frequency,
    string FrequencyUnit,
    HiokiLcrMeasurementLimit PrimaryLimit,
    HiokiLcrMeasurementLimit SecondaryLimit);

/// <summary>
/// Stable local-recipe keys for a HIOKI LCR configuration.
/// They are intentionally independent from a customer's MES field names.
/// </summary>
public static class HiokiLcrRecipeKeys
{
    public const string FrequencyMode = "FrequencyMode";
    public const string LoadType = "LoadType";
    public const string Frequency = "Frequency";
    public const string FrequencyUnit = "FrequencyUnit";
    public const string Voltage = "Voltage";
    public const string VoltageUnit = "VoltageUnit";
    public const string Delay = "Delay";
    public const string Range = "Range";
    public const string Speed = "Speed";
    public const string SecondLoadType = "SecondLoadType";
    public const string Frequency2 = "Frequency2";
    public const string Frequency2Unit = "Frequency2Unit";

    public static string Get(int stationId, string name)
        => $"Instrument.Station{stationId}.HiokiLcr.{name}";
}

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
    public const string OffOff = HiokiLcrConfig.OffOff;
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
    OffOff,
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
            "OFF" or "OFFOFF" => OffOff,
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
            OffOff => 0,
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
            OffOff => new HiokiLcrParameterPair("OFF", "OFF"),
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









