using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Models;

public sealed class StationEnableItemModel : BindableBase
{
    private readonly ILocalizationService localizationService;
    private bool isEnabled;

    public StationEnableItemModel(TestStationModel station, ILocalizationService localizationService)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        isEnabled = station.IsEnabled;
    }

    public TestStationModel Station { get; }

    public int StationId => Station.StationId;

    public string DisplayName => ResolveStationName();

    public string StationDisplayName => GetStationDisplayName();

    public string DeviceDisplayName => GetDeviceDisplayName();

    public bool IsCameraStation => DisplayName.Contains("相机", StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains("相機", StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains("Camera", StringComparison.OrdinalIgnoreCase)
        || DeviceDisplayName.Contains("相机", StringComparison.OrdinalIgnoreCase)
        || DeviceDisplayName.Contains("相機", StringComparison.OrdinalIgnoreCase)
        || DeviceDisplayName.Contains("Camera", StringComparison.OrdinalIgnoreCase)
        || Station.InstrumentDeviceIds.Any(static id => id.Contains("Camera", StringComparison.OrdinalIgnoreCase));

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                OnPropertyChanged(nameof(StateText));
            }
        }
    }

    public string StateText => IsEnabled ? T("Station.State.Enabled", "开启") : T("Station.State.Disabled", "关闭");

    public void SyncFromStation()
        => IsEnabled = Station.IsEnabled;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(StationDisplayName));
        OnPropertyChanged(nameof(DeviceDisplayName));
        OnPropertyChanged(nameof(IsCameraStation));
        OnPropertyChanged(nameof(StateText));
    }

    private string T(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private string ResolveStationName()
        => string.IsNullOrWhiteSpace(Station.StationNameKey)
            ? Station.StationName
            : T(Station.StationNameKey, Station.StationName);

    private string ResolveOptional(string? key, string fallback)
        => string.IsNullOrWhiteSpace(key) ? fallback : T(key, fallback);

    private string GetStationDisplayName()
    {
        string name = ResolveStationName().Trim();
        int splitIndex = name.IndexOf(' ');
        string fallback = splitIndex > 0 ? name[..splitIndex] : name;
        return ResolveOptional(Station.StationShortNameKey, fallback);
    }

    private string GetDeviceDisplayName()
    {
        string explicitDeviceName = ResolveOptional(Station.StationDeviceNameKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(explicitDeviceName))
        {
            return explicitDeviceName;
        }

        if (Station.OrderedTestNames.Count > 0)
        {
            return string.Join(" / ", Station.OrderedTestNames);
        }

        string instrumentNames = string.Join(" / ", Station.StationDataDeals
            .OfType<IStationInstrumentOperation>()
            .SelectMany(operation => SplitTestNames(operation.TestName))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(instrumentNames))
        {
            return instrumentNames;
        }

        string name = DisplayName.Trim();
        int splitIndex = name.IndexOf(' ');
        if (splitIndex >= 0 && splitIndex + 1 < name.Length)
        {
            return name[(splitIndex + 1)..].Trim();
        }

        string? deviceId = Station.InstrumentDeviceIds.FirstOrDefault();
        return string.IsNullOrWhiteSpace(deviceId) ? string.Empty : deviceId;
    }

    private static IEnumerable<string> SplitTestNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}