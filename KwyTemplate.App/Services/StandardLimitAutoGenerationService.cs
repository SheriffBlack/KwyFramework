using System.Globalization;
using Kwy.Converter;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// Converts an offline standard-sample center value into its check limits.
/// The rule source is kept independent from the view so it can be reused by
/// either standard-sample panel.
/// </summary>
public sealed class StandardLimitAutoGenerationService
{
    private readonly StandardLimitAutoGenerationOptionsStore optionsStore;

    public StandardLimitAutoGenerationService(StandardLimitAutoGenerationOptionsStore optionsStore)
    {
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
    }

    public bool TryApply(StandardSampleLimitItemModel item)
        => TryApply(item, optionsStore.Current);

    public static bool TryApply(StandardSampleLimitItemModel item, StandardLimitAutoGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(item.StandardValue))
        {
            return ClearLimits(item);
        }

        if (!TryParse(item.StandardValue, out double centerValue))
        {
            return false;
        }

        return ResolveKind(item.Code) switch
        {
            MeasurementKind.Dcr => ApplyDcr(item, centerValue, options),
            MeasurementKind.Ls => ApplyPercent(item, centerValue, options.LsNegativeDeviationPercent, options.LsPositiveDeviationPercent),
            MeasurementKind.Rs => ApplyRs(item, centerValue, options),
            MeasurementKind.Q => ApplyAbsolute(item, options.QLowerLimit, options.QUpperLimit),
            MeasurementKind.HighLs => ApplyPercent(item, centerValue, options.HighLsNegativeDeviationPercent, options.HighLsPositiveDeviationPercent),
            MeasurementKind.HighRs => ApplyPercent(item, centerValue, options.HighRsNegativeDeviationPercent, options.HighRsPositiveDeviationPercent),
            _ => false
        };
    }

    private static bool ApplyDcr(StandardSampleLimitItemModel item, double centerValue, StandardLimitAutoGenerationOptions options)
    {
        double centerMilliOhm = MeasurementUnitConverter.Convert(centerValue, "DCR", item.Unit, "mΩ");
        return centerMilliOhm <= 50.0
            ? ApplyAbsoluteDeviation(item, centerValue, "DCR", "mΩ", options.DcrAtOrBelow50MilliOhmNegativeDeviation, options.DcrAtOrBelow50MilliOhmPositiveDeviation)
            : ApplyPercent(item, centerValue, options.DcrAbove50MilliOhmNegativeDeviationPercent, options.DcrAbove50MilliOhmPositiveDeviationPercent);
    }

    private static bool ApplyRs(StandardSampleLimitItemModel item, double centerValue, StandardLimitAutoGenerationOptions options)
    {
        double centerOhm = MeasurementUnitConverter.Convert(centerValue, "RS", item.Unit, "Ω");
        return centerOhm <= 0.5
            ? ApplyAbsoluteDeviation(item, centerValue, "RS", "mΩ", options.RsAtOrBelow0Point5OhmNegativeDeviation, options.RsAtOrBelow0Point5OhmPositiveDeviation)
            : ApplyPercent(item, centerValue, options.RsAbove0Point5OhmNegativeDeviationPercent, options.RsAbove0Point5OhmPositiveDeviationPercent);
    }

    private static bool ApplyAbsoluteDeviation(StandardSampleLimitItemModel item, double centerValue, string quantity, string deviationUnit, double? negativeDeviation, double? positiveDeviation)
    {
        double? lowerLimit = negativeDeviation.HasValue
            ? centerValue - MeasurementUnitConverter.Convert(negativeDeviation.Value, quantity, deviationUnit, item.Unit)
            : null;
        double? upperLimit = positiveDeviation.HasValue
            ? centerValue + MeasurementUnitConverter.Convert(positiveDeviation.Value, quantity, deviationUnit, item.Unit)
            : null;
        return ApplyLimits(item, lowerLimit, upperLimit);
    }

    private static bool ApplyPercent(StandardSampleLimitItemModel item, double centerValue, double? negativePercent, double? positivePercent)
        => ApplyLimits(
            item,
            negativePercent.HasValue ? centerValue * (1.0 - negativePercent.Value / 100.0) : null,
            positivePercent.HasValue ? centerValue * (1.0 + positivePercent.Value / 100.0) : null);

    private static bool ApplyAbsolute(StandardSampleLimitItemModel item, double? lowerLimit, double? upperLimit)
        => ApplyLimits(item, lowerLimit, upperLimit);

    private static bool ApplyLimits(StandardSampleLimitItemModel item, double? lowerLimit, double? upperLimit)
    {
        if (!lowerLimit.HasValue && !upperLimit.HasValue)
        {
            return false;
        }

        if (lowerLimit.HasValue)
        {
            item.LowerLimit = Format(lowerLimit.Value);
        }

        if (upperLimit.HasValue)
        {
            item.UpperLimit = Format(upperLimit.Value);
        }

        return true;
    }

    private static bool ClearLimits(StandardSampleLimitItemModel item)
    {
        bool hasLimits = !string.IsNullOrEmpty(item.LowerLimit)
                         || !string.IsNullOrEmpty(item.UpperLimit);
        item.LowerLimit = string.Empty;
        item.UpperLimit = string.Empty;
        return hasLimits;
    }

    private static MeasurementKind ResolveKind(string? code)
    {
        string normalized = (code ?? string.Empty)
            .Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        bool isSecondFrequency = normalized.EndsWith('2');

        while (normalized.Length > 0 && char.IsDigit(normalized[^1]))
        {
            normalized = normalized[..^1];
        }

        if ((normalized.StartsWith("H", StringComparison.Ordinal) || isSecondFrequency)
            && normalized.Contains("LS", StringComparison.Ordinal))
        {
            return MeasurementKind.HighLs;
        }

        if ((normalized.StartsWith("H", StringComparison.Ordinal) || isSecondFrequency)
            && normalized.Contains("RS", StringComparison.Ordinal))
        {
            return MeasurementKind.HighRs;
        }

        return normalized switch
        {
            "DCR" => MeasurementKind.Dcr,
            "LS" => MeasurementKind.Ls,
            "RS" => MeasurementKind.Rs,
            "Q" or "HQ" => MeasurementKind.Q,
            _ => MeasurementKind.None
        };
    }

    private static bool TryParse(string? value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)
           || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string Format(double value)
        => value.ToString("0.##########", CultureInfo.InvariantCulture);

    private enum MeasurementKind
    {
        None,
        Dcr,
        Ls,
        Rs,
        Q,
        HighLs,
        HighRs
    }
}
