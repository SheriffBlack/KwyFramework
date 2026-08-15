using Kwy.Device.Abstractions.Instrument;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.MES.Abstract.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace KwyTemplate.App.ViewModels;

public class StationViewModel : BindableBase, INavigationAware
{
    private readonly MachineBase machine;
    private readonly IAppNotificationService notificationService;
    private readonly StationEnableStateStore stationEnableStateStore;
    private readonly MesConnectionStatus mesConnectionStatus;
    private readonly ILocalizationService localizationService;
    private AsyncDelegateCommand<StationEnableItemModel>? toggleStationEnabledCommand;
    private AsyncDelegateCommand<StationTestItemModel>? triggerStationInstrumentsCommand;
    private int refreshStationEnabledGate;

    public StationViewModel(
        MachineBase machine,
        IAppNotificationService notificationService,
        StationEnableStateStore stationEnableStateStore,
        MesConnectionStatus mesConnectionStatus,
        ILocalizationService localizationService)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.stationEnableStateStore = stationEnableStateStore ?? throw new ArgumentNullException(nameof(stationEnableStateStore));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.mesConnectionStatus.PropertyChanged += OnMesConnectionStatusPropertyChanged;
        this.localizationService.LanguageChanged += OnLanguageChanged;

        foreach (TestStationModel station in machine.TestStations)
        {
            StationTestItems.Add(new StationTestItemModel(station, this.localizationService));
        }

        _ = RefreshStationEnabledStatesAsync();
    }

    public ObservableCollection<StationEnableItemModel> StationItems => stationEnableStateStore.Items;

    public ObservableCollection<StationTestItemModel> StationTestItems { get; } = [];

    public bool CanEditStationEnabled => mesConnectionStatus.State != MesConnectionState.Online;

    public AsyncDelegateCommand<StationEnableItemModel> ToggleStationEnabledCommand
        => toggleStationEnabledCommand ??= new AsyncDelegateCommand<StationEnableItemModel>(ToggleStationEnabledAsync, _ => CanEditStationEnabled);

    public AsyncDelegateCommand<StationTestItemModel> TriggerStationInstrumentsCommand
        => triggerStationInstrumentsCommand ??= new AsyncDelegateCommand<StationTestItemModel>(TriggerStationInstrumentsAsync, CanTriggerStationInstruments);

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshStationEnabledStatesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mesConnectionStatus.PropertyChanged -= OnMesConnectionStatusPropertyChanged;
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        base.Dispose(disposing);
    }

    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        foreach (StationTestItemModel item in StationTestItems)
        {
            item.RefreshLocalization();
        }
    }

    private void OnMesConnectionStatusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(MesConnectionStatus.State), StringComparison.Ordinal)
            && !string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        RaisePropertyChanged(nameof(CanEditStationEnabled));
        toggleStationEnabledCommand?.RaiseCanExecuteChanged();
    }

    private async Task RefreshStationEnabledStatesAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref refreshStationEnabledGate, 1) == 1)
        {
            return;
        }

        try
        {
            await stationEnableStateStore.RefreshFromPlcAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await notificationService.WarningAsync(localizationService.TF("Station.Message.RefreshEnabledFailed", "Refresh station enabled state failed: {0}", ex.Message), localizationService.T("Station.Title.Settings", "Station Settings")).ConfigureAwait(true);
        }
        finally
        {
            Volatile.Write(ref refreshStationEnabledGate, 0);
        }
    }

    private async Task ToggleStationEnabledAsync(StationEnableItemModel? item)
    {
        if (item == null)
        {
            return;
        }

        if (!CanEditStationEnabled)
        {
            item.SyncFromStation();
            await notificationService.WarningAsync(localizationService.T("Station.Message.MesOnlineCannotToggle", "MES is online. Disconnect MES before changing station enabled state."), localizationService.T("Station.Title.Settings", "Station Settings")).ConfigureAwait(true);
            return;
        }

        bool requestedState = item.IsEnabled;
        try
        {
            await stationEnableStateStore.SetStationEnabledAsync(item, requestedState).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            item.IsEnabled = !requestedState;
            item.Station.IsEnabled = item.IsEnabled;
            await notificationService.ErrorAsync(localizationService.TF("Station.Message.SaveEnabledFailed", "Save station enabled state failed: {0}", ex.Message), localizationService.T("Station.Title.Settings", "Station Settings"), ex).ConfigureAwait(true);
        }
    }


    private static bool CanTriggerStationInstruments(StationTestItemModel? item)
        => item?.CanTrigger == true;

    private async Task TriggerStationInstrumentsAsync(StationTestItemModel? item)
    {
        if (item == null || !item.CanTrigger)
        {
            return;
        }

        item.IsBusy = true;
        triggerStationInstrumentsCommand?.RaiseCanExecuteChanged();

        try
        {
            foreach (StationInstrumentItemModel instrument in item.Instruments)
            {
                instrument.Clear();
            }

            foreach (StationInstrumentItemModel instrument in item.Instruments)
            {
                InstrumentMeasurementResult result = await instrument.Operation.MeasureBySoftwareTriggerAsync().ConfigureAwait(true);
                instrument.ApplyMeasurement(result);
            }
        }
        catch (Exception ex)
        {
            await notificationService.ErrorAsync(localizationService.TF("Station.Message.SoftTriggerFailed", "Station {0} soft trigger failed: {1}", item.DisplayName, ex.Message), localizationService.T("Station.Title.SoftTrigger", "Station Soft Trigger"), ex).ConfigureAwait(true);
        }
        finally
        {
            item.IsBusy = false;
            triggerStationInstrumentsCommand?.RaiseCanExecuteChanged();
        }
    }
}


