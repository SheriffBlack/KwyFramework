using System.Collections.ObjectModel;
using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Models;

public sealed class PolarityCheckItemModel : BindableBase
{
    private readonly ILocalizationService localizationService;

    public PolarityCheckItemModel(TestStationModel station, ILocalizationService localizationService)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        for (int i = 0; i < 10; i++)
        {
            ForwardZValues.Add(string.Empty);
            ReverseZValues.Add(string.Empty);
        }
    }

    public TestStationModel Station { get; }

    public int StationId => Station.StationId;

    public string DisplayName => string.IsNullOrWhiteSpace(Station.StationNameKey)
        ? Station.StationName
        : localizationService.T(Station.StationNameKey, Station.StationName);

    public ObservableCollection<string> ForwardZValues { get; } = [];

    public ObservableCollection<string> ReverseZValues { get; } = [];

    public void RefreshLocalization()
        => OnPropertyChanged(nameof(DisplayName));

}
