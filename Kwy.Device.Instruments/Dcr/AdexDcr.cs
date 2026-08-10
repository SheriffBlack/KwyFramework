using System.Diagnostics.CodeAnalysis;
using Kwy.Communicate.Abstractions;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Core.Instrument;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kwy.Device.Instruments.Dcr;

/// <summary>
/// ADEX AX1152D/AX1156 series DCR meter driver.
/// </summary>
public class AdexDcr : 
    InstrumentBase, 
    IMeasurementInstrument, 
    IMeasurementLimitProvider
{
    private const string DefaultModel = "ADEX_DCR";
    private const string CommandTerminator = "\r\n";
    private const string RemoteModeCommand = "PEO02\r\n";
    private const string ReadResultCommand = "DT\r\n";
    private const string TriggerCommand = "E\r\n";
    private const string ExternalTriggerMode = "1";
    private const string BuzzerModeOff = "0";
    private const string OhmUnit = "Ω";
    private const string MilliOhmUnit = "mΩ";
    private const string MicroOhmUnit = "μΩ";
    private const string ModelAx1152 = "AX1152D";
    private const string ModelAx1156A = "AX1156A";

    private static readonly Regex ScientificNumberRegex = new(@"[-+]?\d+(\.\d+)?([eE][-+]?\d+)?", RegexOptions.Compiled);

    public static string MapRange(string range) => range switch
    {
        "1mΩ" => "R0",
        "10mΩ" => "R1",
        "100mΩ" => "R2",
        "1Ω" => "R3",
        "10Ω" => "R4",
        "100Ω" => "R5",
        "1KΩ" => "R6",
        _ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unsupported ADEX DCR range.")
    };

    public static string MapSpeed(string speed) => speed switch
    {
        "FAST" => "1",
        "SLOW" => "0",
        _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unsupported ADEX DCR speed.")
    };

    public static string MapModel(string model) => model switch
    {
        ModelAx1152 => ModelAx1152,
        ModelAx1156A => ModelAx1156A,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported ADEX DCR model.")
    };

    public static double ConvertLimitToOhms(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            OhmUnit => value,
            MilliOhmUnit => value / 1000d,
            MicroOhmUnit => value / 1_000_000d,
            _ => value
        };

    public override string DeviceModel => DefaultModel;

    public AdexDcr(string deviceId, string deviceName, IProtocolConfig protocolConfig, ICommunicationFactory? factory = null)
        : base(deviceId, deviceName, protocolConfig, factory)
    {
    }

    public AdexDcr(string deviceId, string deviceName, ICommunicationClient protocol)
        : base(deviceId, deviceName, protocol)
    {
    }

    /// <summary>
    /// Builds the AX1152D/AX1156 parameter command package.
    /// </summary>
    public override string JoinCommand()
    {
        var config = GetConfig();
        string model = MapModel(config.Model);
        string mappedRange = MapRange(config.Range);
        int lowerLimitRaw = ConvertEngineeringLimitToRaw(config.LowerLimitRaw, config.LowerLimitRawUnit, mappedRange);
        int upperLimitRaw = ConvertEngineeringLimitToRaw(config.UpperLimitRaw, config.UpperLimitRawUnit, mappedRange);
        var builder = new StringBuilder();

        builder.Append('F').Append(config.TestMode);
        builder.Append(mappedRange);
        builder.Append(BuildLimitCommand(lowerLimitRaw, "L", config.TestMode, model));
        builder.Append(BuildLimitCommand(upperLimitRaw, "H", config.TestMode, model));
        builder.Append('T').Append(ExternalTriggerMode);
        builder.Append('W').Append(MapSpeed(config.Speed));
        builder.Append('B').Append(BuzzerModeOff).Append(CommandTerminator);

        return builder.ToString();
    }

    /// <summary>
    /// Sends the command that switches the meter to remote mode.
    /// </summary>
    public ValueTask EnterRemoteModeAsync(CancellationToken cancellationToken = default)
        => WriteCommandAsync(RemoteModeCommand, cancellationToken);

    /// <summary>
    /// Sends the trigger command.
    /// </summary>
    public ValueTask TriggerMeasurementAsync(CancellationToken cancellationToken = default)
        => TriggerAsync(TriggerCommand, cancellationToken);

    /// <summary>
    /// Reads one raw DCR response from the meter.
    /// </summary>
    public ValueTask<string> ReadRawResultAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ReadResultCommand, cancellationToken);

    /// <summary>
    /// Reads and parses one DCR result.
    /// </summary>
    public async ValueTask<AdexDcrResult> ReadResultAsync(CancellationToken cancellationToken = default)
    {
        string response = await ReadRawResultAsync(cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    /// <summary>
    /// Reads and converts one DCR result to the common instrument measurement model.
    /// </summary>
    public async ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default)
    {
        AdexDcrResult result = await ReadResultAsync(cancellationToken).ConfigureAwait(false);
        return ToMeasurementResult(result);
    }

    /// <summary>
    /// Software trigger returns the result directly: E -> read. Do not send DT here;
    /// DT is only used by ReadMeasurementAsync when reading an externally/IO-triggered result.
    /// </summary>
    public async ValueTask<InstrumentMeasurementResult> MeasureBySoftwareTriggerAsync(CancellationToken cancellationToken = default)
    {
        await TriggerMeasurementAsync(cancellationToken).ConfigureAwait(false);
        string response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        return ToMeasurementResult(ParseResult(response));
    }

    private InstrumentMeasurementResult ToMeasurementResult(AdexDcrResult result)
    {
        string mappedRange = MapRange(GetConfig().Range);
        string displayUnit = GetEngineeringUnit(mappedRange);
        return new InstrumentMeasurementResult(
            [new InstrumentMeasurementValue("DCR", ConvertOhmsToRangeUnit(result.Resistance, mappedRange), ToMeasurementJudgment(result.Judgment), Unit: displayUnit)],
            result.RawText);
    }

    /// <summary>
    /// Gets engineering limits for chart display and PC-side judgment.
    /// </summary>
    public bool TryGetMeasurementLimit([NotNullWhen(true)] out InstrumentMeasurementLimit? limit)
    {
        var config = GetConfig();
        string mappedRange = MapRange(config.Range);
        limit = new InstrumentMeasurementLimit(
            ConvertLimitToRangeUnit(config.LowerLimitRaw, config.LowerLimitRawUnit, mappedRange),
            ConvertLimitToRangeUnit(config.UpperLimitRaw, config.UpperLimitRawUnit, mappedRange),
            GetEngineeringUnit(mappedRange));
        return config.LowerLimitRaw > 0 || config.UpperLimitRaw > 0;
    }

    /// <summary>
    /// Compares current device parameters with the raw parameter string returned by the meter.
    /// </summary>
    public bool CompareParameterText(string parameterText)
    {
        if (string.IsNullOrWhiteSpace(parameterText))
        {
            return false;
        }

        var config = GetConfig();
        string mappedRange = MapRange(config.Range);
        int expectedLow = ConvertEngineeringLimitToRaw(config.LowerLimitRaw, config.LowerLimitRawUnit, mappedRange);
        int expectedHigh = ConvertEngineeringLimitToRaw(config.UpperLimitRaw, config.UpperLimitRawUnit, mappedRange);
        string[] parts = parameterText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return false;
        }

        return string.Equals(parts[1], mappedRange, StringComparison.OrdinalIgnoreCase)
            && TryParseLimit(parts[2], out int low) && low == expectedLow
            && TryParseLimit(parts[3], out int high) && high == expectedHigh;
    }

    protected override string ParseResponse(ReadOnlySpan<byte> responseBytes)
        => Encoding.ASCII.GetString(responseBytes).Trim();

    public static AdexDcrResult ParseResult(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new FormatException("ADEX DCR returned an empty response.");
        }

        Match match = ScientificNumberRegex.Match(response);
        if (!match.Success || !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new FormatException($"Cannot parse ADEX DCR response: {response}");
        }

        string[] parts = response.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string judgment = parts.Length > 1 ? parts[1] : string.Empty;
        return new AdexDcrResult(value, judgment, response.Trim());
    }

    private static InstrumentMeasurementJudgment ToMeasurementJudgment(string judgment)
    {
        if (string.IsNullOrWhiteSpace(judgment))
        {
            return InstrumentMeasurementJudgment.Unknown;
        }

        if (judgment.Contains("GO", StringComparison.OrdinalIgnoreCase)
            || judgment.Contains("OK", StringComparison.OrdinalIgnoreCase)
            || judgment.Contains("PASS", StringComparison.OrdinalIgnoreCase))
        {
            return InstrumentMeasurementJudgment.Ok;
        }

        if (judgment.Contains("HI", StringComparison.OrdinalIgnoreCase))
        {
            return InstrumentMeasurementJudgment.High;
        }

        if (judgment.Contains("LO", StringComparison.OrdinalIgnoreCase))
        {
            return InstrumentMeasurementJudgment.Low;
        }

        return InstrumentMeasurementJudgment.Error;
    }

    private AdexDcrConfig GetConfig()
    {
        if (DeviceParameter is AdexDcrConfig config)
        {
            return config;
        }

        throw new InvalidOperationException("ADEX DCR configuration is not set.");
    }

    private static int ConvertEngineeringLimitToRaw(double value, string? unit, string mappedRange)
    {

        double valueInRangeUnit = ConvertLimitToRangeUnit(value, unit, mappedRange);
        ValidateRangeLimit(valueInRangeUnit, mappedRange);
        double raw = mappedRange switch
        {
            "R0" => valueInRangeUnit * 10000d,
            "R1" => valueInRangeUnit * 1000d,
            "R2" => valueInRangeUnit * 100d,
            "R3" => valueInRangeUnit * 10d,
            "R4" => valueInRangeUnit * 1000d,
            "R5" => valueInRangeUnit * 100d,
            "R6" => valueInRangeUnit * 10d,
            _ => throw new ArgumentOutOfRangeException(nameof(mappedRange), mappedRange, "Unsupported ADEX DCR range.")
        };

        int rawValue = Convert.ToInt32(Math.Round(raw, MidpointRounding.AwayFromZero));
        if (rawValue is < 0 or > 99999)
        {
            throw new InvalidOperationException($"ADEX DCR 上下限原始值 {rawValue} 超出 00000~99999 范围。");
        }

        return rawValue;
    }

    private static double ConvertLimitToRangeUnit(double value, string? unit, string mappedRange)
    {
        double valueInOhms = ConvertLimitToOhms(value, unit);
        return ConvertOhmsToRangeUnit(valueInOhms, mappedRange);
    }

    private static double ConvertOhmsToRangeUnit(double valueInOhms, string mappedRange)
        => IsMilliOhmRange(mappedRange) ? valueInOhms * 1000d : valueInOhms;

    private static string GetEngineeringUnit(string mappedRange)
        => IsMilliOhmRange(mappedRange) ? MilliOhmUnit : OhmUnit;

    private static void ValidateRangeLimit(double valueInRangeUnit, string mappedRange)
    {
        double max = mappedRange switch
        {
            "R0" => 1.5d,
            "R1" => 15d,
            "R2" => 150d,
            "R3" => 1500d,
            "R4" => 15d,
            "R5" => 150d,
            "R6" => 1500d,
            _ => double.PositiveInfinity
        };

        if (valueInRangeUnit > max)
        {
            string unit = IsMilliOhmRange(mappedRange) ? MilliOhmUnit : OhmUnit;
            throw new InvalidOperationException($"ADEX DCR {mappedRange} 档范围为 0~{max.ToString(CultureInfo.InvariantCulture)}{unit}。");
        }
    }

    private static bool IsMilliOhmRange(string mappedRange)
        => mappedRange is "R0" or "R1" or "R2" or "R3";

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return OhmUnit;
        }

        string normalized = unit.Trim();
        if (normalized.Equals("uΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("μΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("uohm", StringComparison.OrdinalIgnoreCase))
        {
            return MicroOhmUnit;
        }

        if (normalized.Equals("mΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("mohm", StringComparison.OrdinalIgnoreCase))
        {
            return MilliOhmUnit;
        }

        return OhmUnit;
    }

    private static string BuildLimitCommand(int rawValue, string limitKind, string testMode, string model)
    {
        string head = string.Equals(testMode, "R", StringComparison.OrdinalIgnoreCase) ? "L" : "D";
        string valueText = rawValue.ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');
        return string.Equals(model, ModelAx1156A, StringComparison.OrdinalIgnoreCase)
            ? string.Concat(head, "1", limitKind, valueText)
            : string.Concat(head, limitKind, valueText);
    }

    private static bool TryParseLimit(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string digits = new(text.Where(char.IsDigit).ToArray());
        if (digits.Length >= 5)
        {
            digits = digits[^5..];
        }

        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>
/// ADEX DCR measurement result.
/// </summary>
/// <param name="Resistance">Resistance value returned by the meter.</param>
/// <param name="Judgment">Judgment text returned by the meter, such as GO, HI or LO.</param>
/// <param name="RawText">Original response text.</param>
public sealed record AdexDcrResult(double Resistance, string Judgment, string RawText);





