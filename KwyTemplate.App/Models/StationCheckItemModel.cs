using System.ComponentModel;
using Kwy.MVVM.Core;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Models;

public sealed class StationCheckItemModel : BindableBase
{
    private bool isCompleted;
    private StandardSampleLimitItemModel? standardLimitItem;
    private StandardSampleLimitItemModel? confirmLimitItem;
    private string standardMeasuredValue = string.Empty;
    private string confirmMeasuredValue = string.Empty;
    private bool standardMeasuredValueOutOfRange;
    private bool confirmMeasuredValueOutOfRange;

    public StationCheckItemModel(TestStationModel station, string testName)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        TestName = string.IsNullOrWhiteSpace(testName) ? station.StationName : testName;
    }

    public TestStationModel Station { get; }

    public int StationId => Station.StationId;

    public string TestName { get; }

    public string DisplayName => TestName;

    public string StandardUpperLimit => standardLimitItem?.UpperLimit ?? string.Empty;

    public string StandardLowerLimit => standardLimitItem?.LowerLimit ?? string.Empty;

    public string StandardValue => standardLimitItem?.StandardValue ?? string.Empty;

    public string StandardUnit => standardLimitItem?.Unit ?? string.Empty;

    public string StandardSerialNo => standardLimitItem?.SerialNo ?? string.Empty;

    public string StandardMeterType => standardLimitItem?.MeterType ?? string.Empty;

    public string StandardItemName => standardLimitItem?.ItemName ?? string.Empty;

    public string StandardFrequency => standardLimitItem?.Frequency ?? string.Empty;

    public string StandardFrequencyUnit => standardLimitItem?.FrequencyUnit ?? string.Empty;

    public string StandardMeasuredValue
    {
        get => standardMeasuredValue;
        set => SetProperty(ref standardMeasuredValue, value ?? string.Empty);
    }

    public bool StandardMeasuredValueOutOfRange
    {
        get => standardMeasuredValueOutOfRange;
        set => SetProperty(ref standardMeasuredValueOutOfRange, value);
    }

    public string ConfirmUpperLimit => confirmLimitItem?.UpperLimit ?? string.Empty;

    public string ConfirmLowerLimit => confirmLimitItem?.LowerLimit ?? string.Empty;

    public string ConfirmValue => confirmLimitItem?.StandardValue ?? string.Empty;

    public string ConfirmUnit => confirmLimitItem?.Unit ?? string.Empty;

    public string ConfirmSerialNo => confirmLimitItem?.SerialNo ?? string.Empty;

    public string ConfirmMeterType => confirmLimitItem?.MeterType ?? string.Empty;

    public string ConfirmItemName => confirmLimitItem?.ItemName ?? string.Empty;

    public string ConfirmFrequency => confirmLimitItem?.Frequency ?? string.Empty;

    public string ConfirmFrequencyUnit => confirmLimitItem?.FrequencyUnit ?? string.Empty;

    public string ConfirmMeasuredValue
    {
        get => confirmMeasuredValue;
        set => SetProperty(ref confirmMeasuredValue, value ?? string.Empty);
    }

    public bool ConfirmMeasuredValueOutOfRange
    {
        get => confirmMeasuredValueOutOfRange;
        set => SetProperty(ref confirmMeasuredValueOutOfRange, value);
    }

    public bool IsCompleted
    {
        get => isCompleted;
        set => SetProperty(ref isCompleted, value);
    }

    public void SetStandardLimitItem(StandardSampleLimitItemModel? item)
    {
        if (ReferenceEquals(standardLimitItem, item))
        {
            return;
        }

        if (standardLimitItem != null)
        {
            standardLimitItem.PropertyChanged -= OnStandardLimitItemPropertyChanged;
        }

        standardLimitItem = item;

        if (standardLimitItem != null)
        {
            standardLimitItem.PropertyChanged += OnStandardLimitItemPropertyChanged;
        }

        RaiseStandardLimitPropertiesChanged();
    }

    public void SetConfirmLimitItem(StandardSampleLimitItemModel? item)
    {
        if (ReferenceEquals(confirmLimitItem, item))
        {
            return;
        }

        if (confirmLimitItem != null)
        {
            confirmLimitItem.PropertyChanged -= OnConfirmLimitItemPropertyChanged;
        }

        confirmLimitItem = item;

        if (confirmLimitItem != null)
        {
            confirmLimitItem.PropertyChanged += OnConfirmLimitItemPropertyChanged;
        }

        RaiseConfirmLimitPropertiesChanged();
    }

    private void OnStandardLimitItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StandardSampleLimitItemModel.UpperLimit)
            or nameof(StandardSampleLimitItemModel.LowerLimit)
            or nameof(StandardSampleLimitItemModel.StandardValue)
            or nameof(StandardSampleLimitItemModel.Unit)
            or null
            or "")
        {
            RaiseStandardLimitPropertiesChanged();
        }
    }

    private void OnConfirmLimitItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StandardSampleLimitItemModel.UpperLimit)
            or nameof(StandardSampleLimitItemModel.LowerLimit)
            or nameof(StandardSampleLimitItemModel.StandardValue)
            or nameof(StandardSampleLimitItemModel.Unit)
            or null
            or "")
        {
            RaiseConfirmLimitPropertiesChanged();
        }
    }

    private void RaiseStandardLimitPropertiesChanged()
    {
        RaisePropertyChanged(nameof(StandardUpperLimit));
        RaisePropertyChanged(nameof(StandardLowerLimit));
        RaisePropertyChanged(nameof(StandardValue));
        RaisePropertyChanged(nameof(StandardUnit));
    }

    private void RaiseConfirmLimitPropertiesChanged()
    {
        RaisePropertyChanged(nameof(ConfirmUpperLimit));
        RaisePropertyChanged(nameof(ConfirmLowerLimit));
        RaisePropertyChanged(nameof(ConfirmValue));
        RaisePropertyChanged(nameof(ConfirmUnit));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (standardLimitItem != null)
            {
                standardLimitItem.PropertyChanged -= OnStandardLimitItemPropertyChanged;
            }

            if (confirmLimitItem != null)
            {
                confirmLimitItem.PropertyChanged -= OnConfirmLimitItemPropertyChanged;
            }
        }

        base.Dispose(disposing);
    }
}


