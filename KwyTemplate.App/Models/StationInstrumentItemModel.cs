using System.Globalization;
using Kwy.Device.Abstractions.Instrument;
using Kwy.MVVM.Core;
using KwyTemplate.Flow.DataDeals;

namespace KwyTemplate.App.Models;

public sealed class StationInstrumentItemModel : BindableBase
{
    private string rawValue = string.Empty;
    private string netValue = string.Empty;

    public StationInstrumentItemModel(IStationInstrumentOperation operation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public IStationInstrumentOperation Operation { get; }

    public string TestName => Operation.TestName;

    public string RawValue
    {
        get => rawValue;
        private set => SetProperty(ref rawValue, value);
    }

    public string NetValue
    {
        get => netValue;
        private set => SetProperty(ref netValue, value);
    }

    public void ApplyMeasurement(InstrumentMeasurementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        RawValue = BuildRawValue(result);
        NetValue = BuildNetValue(result);
    }

    public void Clear()
    {
        RawValue = string.Empty;
        NetValue = string.Empty;
    }

    private static string BuildRawValue(InstrumentMeasurementResult result)
        => !string.IsNullOrWhiteSpace(result.RawText)
            ? result.RawText
            : result.Values.FirstOrDefault()?.RawValue ?? string.Empty;

    private static string BuildNetValue(InstrumentMeasurementResult result)
    {
        if (result.Values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", result.Values.Select(FormatValue));
    }

    private static string FormatValue(InstrumentMeasurementValue value)
    {
        // RawValue belongs exclusively to the "原始值" column.  The net value
        // must always use the engineering value already formatted by the
        // instrument operation, otherwise a device raw string can bypass the
        // shared display-unit conversion.
        string text = value.Value.ToString("G8", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(value.Unit))
        {
            text += value.Unit;
        }

        if (string.IsNullOrWhiteSpace(value.Name))
        {
            return text;
        }

        return $"{value.Name}: {text}";
    }
}
