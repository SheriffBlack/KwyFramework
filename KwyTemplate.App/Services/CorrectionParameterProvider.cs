using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// “标准件解析后的 Ls / Rs 标准值、单位、频率”等，整理成 CorrectionParameterSnapshot，供校正页显示和写入仪表参数使用
/// </summary>
public sealed class CorrectionParameterProvider : ICorrectionParameterProvider, IDisposable
{
    private readonly StandardSampleState sampleState;
    private readonly HashSet<StandardSampleLimitItemModel> subscribedItems = [];
    private string frequencyOverride = string.Empty;
    private string frequencyUnitOverride = string.Empty;
    private bool disposed;

    public CorrectionParameterProvider(StandardSampleState sampleState)
    {
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.sampleState.StandardSample.LimitItems.CollectionChanged += OnLimitItemsChanged;
        ReconcileItemSubscriptions();
    }

    public event EventHandler? ParametersChanged;

    public CorrectionParameterSnapshot CreateSnapshot(object? instrumentConfig, bool preferInstrumentFrequency = false)
    {
        StandardSampleLimitItemModel? lsItem = FindLimitItem("LS");
        StandardSampleLimitItemModel? rsItem = FindLimitItem("RS");
        string instrumentFrequency = FormatNullableNumber(GetConfigDouble(instrumentConfig, "Frequency"));
        string instrumentFrequencyUnit = GetConfigString(instrumentConfig, "FrequencyUnit");
        string sampleFrequency = FirstNotEmpty(lsItem?.Frequency, rsItem?.Frequency);
        string sampleFrequencyUnit = FirstNotEmpty(lsItem?.FrequencyUnit, rsItem?.FrequencyUnit);

        string derivedFrequency = preferInstrumentFrequency
            ? FirstNotEmpty(instrumentFrequency, sampleFrequency)
            : FirstNotEmpty(sampleFrequency, instrumentFrequency);
        string derivedFrequencyUnit = preferInstrumentFrequency
            ? FirstNotEmpty(instrumentFrequencyUnit, sampleFrequencyUnit)
            : FirstNotEmpty(sampleFrequencyUnit, instrumentFrequencyUnit);

        return new CorrectionParameterSnapshot(
            lsItem?.StandardValue ?? string.Empty,
            FirstNotEmpty(lsItem?.Unit, GetConfigLimitUnit(instrumentConfig, "LS")),
            rsItem?.StandardValue ?? string.Empty,
            FirstNotEmpty(rsItem?.Unit, GetConfigLimitUnit(instrumentConfig, "RS")),
            FirstNotEmpty(frequencyOverride, derivedFrequency),
            FirstNotEmpty(frequencyUnitOverride, derivedFrequencyUnit),
            FirstNotEmpty(FormatNullableNumber(GetConfigDouble(instrumentConfig, "Voltage")), "1"),
            FirstNotEmpty(GetConfigString(instrumentConfig, "VoltageUnit"), "V"));
    }

    public void SetFrequencyOverride(string frequency, string frequencyUnit)
    {
        frequencyOverride = frequency?.Trim() ?? string.Empty;
        frequencyUnitOverride = frequencyUnit?.Trim() ?? string.Empty;
    }

    public void ClearFrequencyOverride()
    {
        frequencyOverride = string.Empty;
        frequencyUnitOverride = string.Empty;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sampleState.StandardSample.LimitItems.CollectionChanged -= OnLimitItemsChanged;
        foreach (StandardSampleLimitItemModel item in subscribedItems)
        {
            item.PropertyChanged -= OnLimitItemPropertyChanged;
        }

        subscribedItems.Clear();
    }

    private void OnLimitItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReconcileItemSubscriptions();
        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLimitItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StandardSampleLimitItemModel.StandardValue)
            or nameof(StandardSampleLimitItemModel.Unit)
            or nameof(StandardSampleLimitItemModel.Frequency)
            or nameof(StandardSampleLimitItemModel.FrequencyUnit))
        {
            ParametersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReconcileItemSubscriptions()
    {
        var currentItems = sampleState.StandardSample.LimitItems.ToHashSet();
        foreach (StandardSampleLimitItemModel removedItem in subscribedItems.Except(currentItems).ToArray())
        {
            removedItem.PropertyChanged -= OnLimitItemPropertyChanged;
            subscribedItems.Remove(removedItem);
        }

        foreach (StandardSampleLimitItemModel addedItem in currentItems.Except(subscribedItems))
        {
            addedItem.PropertyChanged += OnLimitItemPropertyChanged;
            subscribedItems.Add(addedItem);
        }
    }

    private StandardSampleLimitItemModel? FindLimitItem(string code)
        => sampleState.StandardSample.LimitItems.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

    private static string GetConfigString(object? config, string propertyName)
        => config?.GetType().GetProperty(propertyName)?.GetValue(config)?.ToString() ?? string.Empty;

    private static string GetConfigLimitUnit(object? config, string parameterName)
    {
        if (config == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return string.Empty;
        }

        if (IsConfigParameter(config, "Parameter1DisplayName", parameterName))
        {
            return FirstNotEmpty(GetConfigString(config, "Parameter1LowerLimitUnit"), GetConfigString(config, "Parameter1UpperLimitUnit"));
        }

        if (IsConfigParameter(config, "Parameter3DisplayName", parameterName))
        {
            return FirstNotEmpty(GetConfigString(config, "Parameter3LowerLimitUnit"), GetConfigString(config, "Parameter3UpperLimitUnit"));
        }

        return string.Empty;
    }

    private static bool IsConfigParameter(object config, string propertyName, string parameterName)
        => string.Equals(GetConfigString(config, propertyName), parameterName, StringComparison.OrdinalIgnoreCase);

    private static double? GetConfigDouble(object? config, string propertyName)
    {
        object? value = config?.GetType().GetProperty(propertyName)?.GetValue(config);
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            decimal decimalValue => (double)decimalValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) => result,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double result) => result,
            _ => null
        };
    }

    private static string FormatNullableNumber(double? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FirstNotEmpty(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
