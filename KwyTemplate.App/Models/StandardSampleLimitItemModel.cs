using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Models;

public sealed class StandardSampleLimitItemModel : BindableBase
{
    private readonly ILocalizationService? localizationService;
    private string lowerLimit = string.Empty;
    private string upperLimit = string.Empty;
    private string standardValue = string.Empty;
    private string unit = string.Empty;
    private string serialNo = string.Empty;
    private string meterType = string.Empty;
    private string itemName = string.Empty;
    private string frequency = string.Empty;
    private string frequencyUnit = string.Empty;

    public StandardSampleLimitItemModel(string code, string displayName, ILocalizationService? localizationService = null)
    {
        Code = code;
        DisplayName = displayName;
        this.localizationService = localizationService;
    }

    public string Code { get; }

    public string DisplayName { get; }

    public string LimitTitle => localizationService?.TF("Standard.Field.LimitRange", "{0}下限~上限", DisplayName)
        ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}下限~上限", DisplayName);

    public string LowerLimit
    {
        get => lowerLimit;
        set => SetProperty(ref lowerLimit, value ?? string.Empty);
    }

    public string UpperLimit
    {
        get => upperLimit;
        set => SetProperty(ref upperLimit, value ?? string.Empty);
    }

    public string StandardValue
    {
        get => standardValue;
        set => SetProperty(ref standardValue, value ?? string.Empty);
    }

    public string Unit
    {
        get => unit;
        set => SetProperty(ref unit, value ?? string.Empty);
    }

    public string SerialNo
    {
        get => serialNo;
        set => SetProperty(ref serialNo, value ?? string.Empty);
    }

    public string MeterType
    {
        get => meterType;
        set => SetProperty(ref meterType, value ?? string.Empty);
    }

    public string ItemName
    {
        get => itemName;
        set => SetProperty(ref itemName, value ?? string.Empty);
    }

    public string Frequency
    {
        get => frequency;
        set => SetProperty(ref frequency, value ?? string.Empty);
    }

    public string FrequencyUnit
    {
        get => frequencyUnit;
        set => SetProperty(ref frequencyUnit, value ?? string.Empty);
    }

    public void RefreshLocalization()
        => OnPropertyChanged(nameof(LimitTitle));

    public void ClearValue()
    {
        LowerLimit = string.Empty;
        UpperLimit = string.Empty;
        StandardValue = string.Empty;
        Unit = string.Empty;
        SerialNo = string.Empty;
        MeterType = string.Empty;
        ItemName = string.Empty;
        Frequency = string.Empty;
        FrequencyUnit = string.Empty;
    }


}
