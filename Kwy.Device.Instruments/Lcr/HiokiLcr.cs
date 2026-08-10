using System.Diagnostics.CodeAnalysis;
using Kwy.Communicate.Abstractions;
using Kwy.Device.Core.Instrument;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Instrument;
using System.Globalization;
using System.Text;

namespace Kwy.Device.Instruments.Lcr;

/// <summary>
/// HIOKI LCR meter driver for IM3533, IM3570, IM3536 and compatible models.
/// </summary>
public class HiokiLcr : 
    InstrumentBase, 
    IMeasurementInstrument, 
    IInstrumentCorrection, 
    IMeasurementLimitSetProvider,
    IMeasurementDisplayFormatter
{
    private const string DefaultModel = "HIOKI_LCR";
    private const string TriggerCommand = "*TRG;\n";
    private const string ReadResultCommand = ":MEAS?\n";
    private const string ReadAllResultCommand = ":MEAS? ALL\n";
    private const string OperationCompleteQueryCommand = "*OPC?\n";
    private const string ComparatorOn = "ON";
    private const string ExternalTriggerMode = "EXT";
    private static readonly TimeSpan CorrectionOperationCompleteTimeout = TimeSpan.FromSeconds(15);
    private string? activeLoadCorrectionType;

    public override string DeviceModel => DefaultModel;

    public IReadOnlyList<string> SupportedLoadCorrectionTypes => HiokiLcrLoadTypes.All;

    public string DefaultLoadCorrectionType => GetConfig().LoadType;

    public bool TryGetMeasurementLimits([NotNullWhen(true)] out IReadOnlyDictionary<string, InstrumentMeasurementLimit>? limits)
    {
        if (DeviceParameter is not HiokiLcrConfig config)
        {
            limits = new Dictionary<string, InstrumentMeasurementLimit>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        HiokiLcrParameterPair activeParameters = config.GetActiveParameterPair();
        var result = new Dictionary<string, InstrumentMeasurementLimit>(StringComparer.OrdinalIgnoreCase);
        AddMeasurementLimit(result, activeParameters.Parameter1, config.Parameter1Min, config.Parameter1Max, config.Parameter1MinUnit);
        AddMeasurementLimit(result, activeParameters.Parameter3, config.Parameter3Min, config.Parameter3Max, config.Parameter3MinUnit);

        limits = result;
        return result.Count > 0;
    }

    private static void AddMeasurementLimit(
        IDictionary<string, InstrumentMeasurementLimit> limits,
        string parameter,
        double lowerLimit,
        double upperLimit,
        string? unit)
    {
        string testName = HiokiLcrLoadTypes.ToDisplayParameter(parameter);
        if (string.IsNullOrWhiteSpace(testName))
        {
            return;
        }

        limits[testName] = new InstrumentMeasurementLimit(lowerLimit, upperLimit, unit);
    }

    public HiokiLcr(string deviceId, string deviceName, IProtocolConfig protocolConfig, ICommunicationFactory? factory = null)
        : base(deviceId, deviceName, protocolConfig, factory)
    {
    }

    public HiokiLcr(string deviceId, string deviceName, ICommunicationClient protocol)
        : base(deviceId, deviceName, protocol)
    {
    }

    /// <summary>
    /// Builds the HIOKI LCR parameter command package.
    /// </summary>
    public override string JoinCommand()
    {
        if (DeviceParameter is not HiokiLcrConfig config)
        {
            return string.Empty;
        }

        HiokiLcrParameterPair activeParameters = config.GetActiveParameterPair();

        var builder = new StringBuilder();
        builder.Append(":MODE LCR;");
        builder.Append(CultureInfo.InvariantCulture, $":PARameter1 {MapParameter(activeParameters.Parameter1)};");
        builder.Append(":PARameter2 OFF;");
        builder.Append(CultureInfo.InvariantCulture, $":PARameter3 {MapParameter(activeParameters.Parameter3)};");
        builder.Append(":PARameter4 OFF;");
        builder.Append(CultureInfo.InvariantCulture, $":COMParator {ComparatorOn};");
        builder.Append(CultureInfo.InvariantCulture, $":SPEEd {config.Speed};");
        builder.Append(CultureInfo.InvariantCulture, $":DCResistance:SPEEd {config.Speed};");
        builder.Append(CultureInfo.InvariantCulture, $":FREQuency {FormatEngineeringValue(config.Frequency, config.FrequencyUnit)};");
        builder.Append(CultureInfo.InvariantCulture, $":LEVel:VOLTage {FormatEngineeringValue(config.Voltage, config.VoltageUnit)};");
        builder.Append(CultureInfo.InvariantCulture, $":TRIGger {ExternalTriggerMode};");
        builder.Append(CultureInfo.InvariantCulture, $":TRIGger:DELay {FormatNumber(config.Delay)};");
        builder.Append(CultureInfo.InvariantCulture, $":RANGe {MapRange(config.Range)};");
        builder.Append(CultureInfo.InvariantCulture, $":COMPARATOR:FLIMIT:ABSOLUTE {FormatMeasurementLimit(config.Parameter1Min, config.Parameter1MinUnit, activeParameters.Parameter1)},{FormatMeasurementLimit(config.Parameter1Max, config.Parameter1MaxUnit, activeParameters.Parameter1)};");
        builder.Append(CultureInfo.InvariantCulture, $":COMPARATOR:SLIMIT:ABSOLUTE {FormatMeasurementLimit(config.Parameter3Min, config.Parameter3MinUnit, activeParameters.Parameter3)},{FormatMeasurementLimit(config.Parameter3Max, config.Parameter3MaxUnit, activeParameters.Parameter3)};");

        return builder.ToString();
    }

    /// <summary>
    /// Sends the trigger command.
    /// </summary>
    public ValueTask TriggerMeasurementAsync(CancellationToken cancellationToken = default)
        => TriggerAsync(TriggerCommand, cancellationToken);

    /// <summary>
    /// Reads one raw HIOKI LCR response.
    /// </summary>
    public ValueTask<string> ReadRawResultAsync(CancellationToken cancellationToken = default)
        => QueryLcrCommandAsync(ReadResultCommand, cancellationToken);

    /// <summary>
    /// Reads one raw HIOKI LCR response by using MEAS? ALL.
    /// </summary>
    public ValueTask<string> ReadAllRawResultAsync(CancellationToken cancellationToken = default)
        => QueryLcrCommandAsync(ReadAllResultCommand, cancellationToken);

    /// <summary>
    /// Reads and parses one HIOKI LCR result.
    /// </summary>
    public async ValueTask<HiokiLcrResult> ReadResultAsync(CancellationToken cancellationToken = default)
    {
        string response = await ReadRawResultAsync(cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    /// <summary>
    /// Reads and converts one HIOKI result to the common instrument measurement model.
    /// </summary>
    public async ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default)
    {
        HiokiLcrResult result = await ReadResultAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<HiokiLcrValue> sourceValues = result.Values;
        var values = new InstrumentMeasurementValue[sourceValues.Count];
        for (int index = 0; index < sourceValues.Count; index++)
        {
            HiokiLcrValue source = sourceValues[index];
            values[index] = new InstrumentMeasurementValue(
                GetMeasurementName(source.ValueIndex),
                source.Value,
                ToMeasurementJudgment(source.Judgment),
                source.RawValue);
        }

        return new InstrumentMeasurementResult(values, result.RawText);
    }

    public InstrumentMeasurementResult ToDisplayMeasurement(InstrumentMeasurementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IReadOnlyList<InstrumentMeasurementValue> sourceValues = result.Values;
        var values = new InstrumentMeasurementValue[sourceValues.Count];
        for (int index = 0; index < sourceValues.Count; index++)
        {
            InstrumentMeasurementValue source = sourceValues[index];
            int valueIndex = index + 1;
            string parameter = GetMeasurementParameter(valueIndex);
            string displayName = MapDisplayParameter(parameter);
            string displayUnit = GetMeasurementDisplayUnit(valueIndex);
            values[index] = new InstrumentMeasurementValue(
                string.IsNullOrWhiteSpace(displayName) ? source.Name : displayName,
                ConvertMeasurementValueFromBaseUnit(source.Value, displayUnit, parameter),
                source.Judgment,
                source.RawValue,
                displayUnit);
        }

        return new InstrumentMeasurementResult(values, result.RawText);
    }

    /// <summary>
    /// Reads and parses one HIOKI LCR result by using MEAS? ALL.
    /// </summary>
    public async ValueTask<HiokiLcrResult> ReadAllResultAsync(CancellationToken cancellationToken = default)
    {
        string response = await ReadAllRawResultAsync(cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    public async ValueTask ExecuteOpenCorrectionAsync(InstrumentCorrectionConditionRequest? request = null, CancellationToken cancellationToken = default)
    {
        HiokiLcrConfig config = GetConfig();
        string frequency = FormatEngineeringValue(request?.Frequency ?? config.Frequency, request?.FrequencyUnit ?? config.FrequencyUnit);
        int spot = GetCorrectionSpot(request?.Spot);
        await ExecuteCorrectionAsync(
            "OpenCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($":CORRection:OPEN:FREQuency {spot},{frequency}", token).ConfigureAwait(false);
                await WriteLcrCommandAsync(":CORRection:OPEN SPOT", token).ConfigureAwait(false);
                await WaitForOperationCompleteAsync("OpenCorrection", token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<InstrumentCorrectionData> ReadOpenCorrectionAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteCorrectionAsync(
            "ReadOpenCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                return ParseSpotCorrectionData(
                    await QueryLcrCommandAsync(":CORRection:OPEN:DATA:SPOT? 1\n", token).ConfigureAwait(false),
                    "Ls",
                    "Rs",
                    true);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ExecuteShortCorrectionAsync(InstrumentCorrectionConditionRequest? request = null, CancellationToken cancellationToken = default)
    {
        HiokiLcrConfig config = GetConfig();
        string frequency = FormatEngineeringValue(request?.Frequency ?? config.Frequency, request?.FrequencyUnit ?? config.FrequencyUnit);
        int spot = GetCorrectionSpot(request?.Spot);
        await ExecuteCorrectionAsync(
            "ShortCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($":CORRection:SHORT:FREQuency {spot},{frequency}", token).ConfigureAwait(false);
                await WriteLcrCommandAsync(":CORRection:SHORT SPOT", token).ConfigureAwait(false);
                await WaitForOperationCompleteAsync("ShortCorrection", token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<InstrumentCorrectionData> ReadShortCorrectionAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteCorrectionAsync(
            "ReadShortCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                return ParseSpotCorrectionData(
                    await QueryLcrCommandAsync(":CORRection:SHORT:DATA:SPOT? 1\n", token).ConfigureAwait(false),
                    "Ls",
                    "Rs",
                    true);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ExecuteLoadCorrectionAsync(InstrumentLoadCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        HiokiLcrConfig config = GetConfig();
        string frequency = FormatEngineeringValue(request.Frequency ?? config.Frequency, request.FrequencyUnit ?? config.FrequencyUnit);
        string voltage = FormatEngineeringValue(request.Voltage ?? config.Voltage, request.VoltageUnit ?? config.VoltageUnit);
        string range = MapRange(string.IsNullOrWhiteSpace(request.Range) ? config.Range : request.Range.Trim());
        string loadType = string.IsNullOrWhiteSpace(request.LoadType) ? config.LoadType : request.LoadType.Trim();
        activeLoadCorrectionType = loadType;
        int modeNo = HiokiLcrLoadTypes.GetModeNo(loadType);
        int spot = request.Spot <= 0 ? 1 : request.Spot;

        await ExecuteCorrectionAsync(
            "LoadCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($":LOAD {spot}", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($":CORRection:LOAD:CONDition {spot},{frequency},{range},OFF,V,{voltage},OFF,0", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($"SAVE {spot},100k", token).ConfigureAwait(false);
                await WriteLcrCommandAsync($":CORRection:LOAD:REFerence {spot},{modeNo},{FormatNumber(request.PrimaryReferenceValue)},{FormatNumber(request.SecondaryReferenceValue)}", token).ConfigureAwait(false);
                await WriteLcrCommandAsync(":CORRection:LOAD:EXECute", token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask EnableLoadCorrectionAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteCorrectionAsync(
            "EnableLoadCorrection",
            token => WriteLcrCommandAsync(":CORRection:LOAD ON", token),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<InstrumentCorrectionData> ReadLoadCorrectionAsync(CancellationToken cancellationToken = default)
    {
        HiokiLcrParameterPair pair = HiokiLcrLoadTypes.Resolve(activeLoadCorrectionType ?? GetConfig().LoadType);
        return await ExecuteCorrectionAsync(
            "ReadLoadCorrection",
            async token =>
            {
                await WriteLcrCommandAsync(":MODE LCR", token).ConfigureAwait(false);
                return ParseSpotCorrectionData(
                    await QueryLcrCommandAsync(":CORRection:LOAD:DATA? 1\n", token).ConfigureAwait(false),
                    MapDisplayParameter(pair.Parameter1),
                    MapDisplayParameter(pair.Parameter3),
                    false);
            },
            cancellationToken).ConfigureAwait(false);
    }


    private async ValueTask WaitForOperationCompleteAsync(string operationName, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CorrectionOperationCompleteTimeout);

        string response = await QueryLcrCommandAsync(OperationCompleteQueryCommand, timeoutCts.Token).ConfigureAwait(false);
        if (string.Equals(response.Trim(), "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException($"HIOKI LCR operation did not complete successfully. Operation={operationName}, Response={response}");
    }
    private static int GetCorrectionSpot(int? spot)
        => spot.GetValueOrDefault() <= 0 ? 1 : spot.GetValueOrDefault();

    private ValueTask WriteLcrCommandAsync(string command, CancellationToken cancellationToken)
        => WriteCommandAsync(EnsureCommandTerminator(command), cancellationToken);
    private async ValueTask<string> QueryLcrCommandAsync(string command, CancellationToken cancellationToken)
    {
        string normalizedCommand = EnsureCommandTerminator(command);
        if (protocol is ICommandQueryClient queryClient)
        {
            return (await queryClient.QueryAsync(normalizedCommand, cancellationToken).ConfigureAwait(false)).Trim();
        }

        return await QueryAsync(normalizedCommand, cancellationToken).ConfigureAwait(false);
    }

    private static string EnsureCommandTerminator(string command)
        => command.EndsWith("\n", StringComparison.Ordinal) ? command : command + "\n";
    protected override string ParseResponse(ReadOnlySpan<byte> responseBytes)
        => Encoding.ASCII.GetString(responseBytes).Trim();

    public static HiokiLcrResult ParseResult(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new FormatException("HIOKI LCR returned an empty response.");
        }

        var values = new List<HiokiLcrValue>();
        string[] groups = response.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            ParseGroup(groups[groupIndex], groupIndex + 1, values);
        }

        if (values.Count == 0)
        {
            throw new FormatException($"Cannot parse HIOKI LCR response: {response}");
        }

        return new HiokiLcrResult(values, response.Trim());
    }

    private string GetMeasurementName(int valueIndex)
    {
        string parameter = GetMeasurementParameter(valueIndex);
        string displayName = MapDisplayParameter(parameter);
        return string.IsNullOrWhiteSpace(displayName) ? $"Value{valueIndex}" : displayName;
    }

    private string GetMeasurementParameter(int valueIndex)
    {
        HiokiLcrParameterPair activeParameters = GetConfig().GetActiveParameterPair();
        return valueIndex switch
        {
            1 => activeParameters.Parameter1,
            2 => activeParameters.Parameter3,
            _ => string.Empty
        };
    }

    private string GetMeasurementDisplayUnit(int valueIndex)
    {
        HiokiLcrConfig config = GetConfig();
        return valueIndex switch
        {
            1 => config.Parameter1MinUnit,
            2 => config.Parameter3MinUnit,
            _ => string.Empty
        };
    }

    private static InstrumentMeasurementJudgment ToMeasurementJudgment(HiokiJudgment judgment)
        => judgment switch
        {
            HiokiJudgment.Ok => InstrumentMeasurementJudgment.Ok,
            HiokiJudgment.High => InstrumentMeasurementJudgment.High,
            HiokiJudgment.Low => InstrumentMeasurementJudgment.Low,
            HiokiJudgment.Error => InstrumentMeasurementJudgment.Error,
            _ => InstrumentMeasurementJudgment.Unknown
        };

    private static void ParseGroup(string group, int groupIndex, ICollection<HiokiLcrValue> values)
    {
        string[] fields = group.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
        {
            return;
        }

        if (fields.Length >= 5)
        {
            AddValue(values, groupIndex, 1, fields[1], fields[2]);
            AddValue(values, groupIndex, 2, fields[3], fields[4]);
            return;
        }

        AddValue(values, groupIndex, 1, fields[1], fields[2]);
    }

    private static void AddValue(ICollection<HiokiLcrValue> values, int groupIndex, int valueIndex, string valueText, string judgmentText)
    {
        values.Add(new HiokiLcrValue(
            groupIndex,
            valueIndex,
            ParseNumber(valueText),
            ParseJudgment(judgmentText),
            valueText));
    }

    private static double ParseNumber(string text)
    {
        if (text.Contains("E+28", StringComparison.OrdinalIgnoreCase))
        {
            return -999999D;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        throw new FormatException($"Cannot parse HIOKI LCR numeric value: {text}");
    }

    private static HiokiJudgment ParseJudgment(string text)
    {
        int sign = 1;
        int value = 0;
        bool hasDigit = false;
        foreach (char character in text)
        {
            if (character == '-')
            {
                sign = -1;
                continue;
            }

            if (character == '+')
            {
                continue;
            }

            if (!char.IsDigit(character))
            {
                continue;
            }

            hasDigit = true;
            value = checked(value * 10 + (character - '0'));
        }

        if (!hasDigit)
        {
            return HiokiJudgment.Error;
        }

        return (sign * value) switch
        {
            0 => HiokiJudgment.Ok,
            1 => HiokiJudgment.High,
            -1 => HiokiJudgment.Low,
            _ => HiokiJudgment.Error
        };
    }

    private HiokiLcrConfig GetConfig()
        => DeviceParameter as HiokiLcrConfig
            ?? throw new InvalidOperationException("HIOKI LCR configuration is not set.");

    private async ValueTask ExecuteCorrectionAsync(
        string operationName,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            RaiseOperationOccurred(
                DeviceOperationKind.Correction,
                operationName,
                true,
                $"Instrument correction succeeded. Device={DeviceName}, Operation={operationName}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationOccurred(
                DeviceOperationKind.Correction,
                operationName,
                false,
                $"Instrument correction failed. Device={DeviceName}, Operation={operationName}, Error={ex.Message}",
                ex);
            throw;
        }
    }

    private async ValueTask<T> ExecuteCorrectionAsync<T>(
        string operationName,
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            T result = await operation(cancellationToken).ConfigureAwait(false);
            RaiseOperationOccurred(
                DeviceOperationKind.Correction,
                operationName,
                true,
                $"Instrument correction succeeded. Device={DeviceName}, Operation={operationName}.");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationOccurred(
                DeviceOperationKind.Correction,
                operationName,
                false,
                $"Instrument correction failed. Device={DeviceName}, Operation={operationName}, Error={ex.Message}",
                ex);
            throw;
        }
    }

    private static InstrumentCorrectionData ParseSpotCorrectionData(string response, string primaryName, string secondaryName, bool skipStatusField)
    {
        string[] fields = response.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int primaryIndex = skipStatusField ? 1 : 0;
        int secondaryIndex = skipStatusField ? 2 : 1;
        if (fields.Length <= secondaryIndex)
        {
            throw new FormatException($"Cannot parse HIOKI correction response: {response}");
        }

        return new InstrumentCorrectionData(
            ParseNumber(fields[primaryIndex]),
            ParseNumber(fields[secondaryIndex]),
            response.Trim(),
            primaryName,
            secondaryName);
    }


    private static string MapDisplayParameter(string parameter)
        => HiokiLcrLoadTypes.ToDisplayParameter(parameter);

    private static string MapParameter(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return "OFF";
        }

        return parameter.ToUpperInvariant() switch
        {
            "L_S" => "LS",
            "L_P" => "LP",
            "C_S" => "CS",
            "C_P" => "CP",
            "R_S" => "RS",
            "R_P" => "RP",
            "PHAS" => "PHASE",
            _ => parameter.ToUpperInvariant()
        };
    }

    private static string MapRange(string range)
        => NormalizeRange(range) switch
        {
            "100mOHM" => "1",
            "1OHM" => "2",
            "10OHM" => "3",
            "300OHM" => "4",
            "1KOHM" => "5",
            "3KOHM" => "6",
            "10KOHM" => "7",
            "30KOHM" => "8",
            "100KOHM" => "9",
            "1MOHM" => "10",
            "10MOHM" => "11",
            "100MOHM" => "12",
            _ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unsupported HIOKI LCR range.")
        };

    private static string NormalizeRange(string range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            throw new ArgumentOutOfRangeException(nameof(range), range, "HIOKI LCR range cannot be empty.");
        }

        return range.Trim()
            .Replace("\u2126", "\u03a9", StringComparison.Ordinal)
            .Replace("\u60df", "\u03a9", StringComparison.Ordinal)
            .Replace("\u03a9", "OHM", StringComparison.Ordinal)
            .Replace("kohm", "KOHM", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatMeasurementLimit(double value, string unit, string parameter)
        => FormatNumber(ConvertMeasurementLimitToBaseUnit(value, unit, parameter));

    private static double ConvertMeasurementLimitToBaseUnit(double value, string? unit, string parameter)
        => NormalizeParameter(parameter) switch
        {
            "LS" or "LP" => ConvertInductanceToHenries(value, unit),
            "Z" or "RS" or "RP" or "X" => ConvertResistanceToOhms(value, unit),
            "CS" or "CP" => ConvertCapacitanceToFarads(value, unit),
            "PHASE" or "PHAS" => value,
            _ => value
        };

    private static double ConvertMeasurementValueFromBaseUnit(double value, string? unit, string parameter)
        => NormalizeParameter(parameter) switch
        {
            "LS" or "LP" => ConvertHenriesToDisplayUnit(value, unit),
            "Z" or "RS" or "RP" or "X" => ConvertOhmsToDisplayUnit(value, unit),
            "CS" or "CP" => ConvertFaradsToDisplayUnit(value, unit),
            "PHASE" or "PHAS" => value,
            _ => value
        };

    private static double ConvertOhmsToDisplayUnit(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MOHM" => value * 1_000D,
            "UOHM" => value * 1_000_000D,
            _ => value
        };

    private static double ConvertHenriesToDisplayUnit(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MH" => value * 1_000D,
            "UH" => value * 1_000_000D,
            "NH" => value * 1_000_000_000D,
            _ => value
        };

    private static double ConvertFaradsToDisplayUnit(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MF" => value * 1_000D,
            "UF" => value * 1_000_000D,
            "NF" => value * 1_000_000_000D,
            "PF" => value * 1_000_000_000_000D,
            _ => value
        };

    private static string NormalizeParameter(string? parameter)
        => string.IsNullOrWhiteSpace(parameter)
            ? string.Empty
            : parameter.Trim()
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();

    private static double ConvertResistanceToOhms(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MOHM" => value / 1_000D,
            "UOHM" => value / 1_000_000D,
            _ => value
        };

    private static double ConvertInductanceToHenries(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MH" => value / 1_000D,
            "UH" => value / 1_000_000D,
            "NH" => value / 1_000_000_000D,
            _ => value
        };

    private static double ConvertCapacitanceToFarads(double value, string? unit)
        => NormalizeUnit(unit) switch
        {
            "MF" => value / 1_000D,
            "UF" => value / 1_000_000D,
            "NF" => value / 1_000_000_000D,
            "PF" => value / 1_000_000_000_000D,
            _ => value
        };

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        string value = unit.Trim()
            .Replace("\u2126", "\u03a9", StringComparison.Ordinal)
            .Replace("\u60df", "\u03a9", StringComparison.Ordinal)
            .Replace("\u00b5", "\u03bc", StringComparison.Ordinal)
            .Replace("\u6e2d", "\u03bc", StringComparison.Ordinal)
            .Replace("u", "\u03bc", StringComparison.OrdinalIgnoreCase);

        return value switch
        {
            "Hz" or "HZ" or "hz" => "HZ",
            "kHz" or "KHZ" or "khz" => "KHZ",
            "MHz" or "MHZ" or "mhz" => "MHZ",
            "mV" => "MV",
            "V" or "v" => "V",
            "\u03a9" or "ohm" or "OHM" => "OHM",
            "m\u03a9" or "mohm" or "MOHM" => "MOHM",
            "\u03bc\u03a9" or "\u03bcohm" or "UOHM" => "UOHM",
            "H" or "h" => "H",
            "mH" => "MH",
            "\u03bcH" => "UH",
            "nH" => "NH",
            "F" or "f" => "F",
            "mF" => "MF",
            "\u03bcF" => "UF",
            "nF" => "NF",
            "pF" => "PF",
            _ => value.ToUpperInvariant()
        };
    }

    private static string FormatEngineeringValue(double value, string unit)
        => NormalizeUnit(unit) switch
        {
            "HZ" => FormatNumber(value),
            "KHZ" => FormatNumber(value * 1_000D),
            "MHZ" => FormatNumber(value * 1_000_000D),
            "MV" => FormatNumber(value / 1_000D),
            "V" => FormatNumber(value),
            _ => FormatNumber(value)
        };

    private static string FormatNumber(double value)
        => value.ToString("G17", CultureInfo.InvariantCulture); 
}

public sealed record HiokiLcrResult(IReadOnlyList<HiokiLcrValue> Values, string RawText);

public sealed record HiokiLcrValue(int GroupIndex, int ValueIndex, double Value, HiokiJudgment Judgment, string RawValue);

public enum HiokiJudgment
{
    Unknown,
    Ok,
    High,
    Low,
    Error
}


