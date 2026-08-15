using System.Collections.ObjectModel;
using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Models;

public sealed class StationTestItemModel : BindableBase
{
    private readonly ILocalizationService localizationService;
    private bool isBusy;

    public StationTestItemModel(TestStationModel station, ILocalizationService localizationService)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        foreach (IStationInstrumentOperation operation in station.StationDataDeals.OfType<IStationInstrumentOperation>())
        {
            Instruments.Add(new StationInstrumentItemModel(operation));
        }
    }

    public TestStationModel Station { get; }

    public int StationId => Station.StationId;

    public string DisplayName => string.IsNullOrWhiteSpace(Station.StationNameKey)
        ? Station.StationName
        : localizationService.T(Station.StationNameKey, Station.StationName);

    public ObservableCollection<StationInstrumentItemModel> Instruments { get; } = [];

    public bool HasInstruments => Instruments.Count > 0;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaisePropertyChanged(nameof(CanTrigger));
            }
        }
    }

    public bool CanTrigger => HasInstruments && !IsBusy;

    public void RefreshLocalization()
        => RaisePropertyChanged(nameof(DisplayName));

}
