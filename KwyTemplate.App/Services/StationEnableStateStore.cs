using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using KwyTemplate.App.Models;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// 统一维护工位启用状态。
/// HomeView 和 StationView 都绑定同一批对象，避免两个界面各自投影后状态不同步。
/// </summary>
public sealed class StationEnableStateStore : IDisposable
{
    private readonly MachineBase machine;
    private readonly IDevice? mainPlc;
    private readonly ILocalizationService localizationService;
    private readonly ObservableCollection<StationEnableItemModel> items = [];
    private int refreshGate;
    private bool disposed;

    public StationEnableStateStore(MachineBase machine, IMachineDeviceContext devices, ILocalizationService localizationService)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        ArgumentNullException.ThrowIfNull(devices);
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        foreach (TestStationModel station in machine.TestStations)
        {
            items.Add(new StationEnableItemModel(station, this.localizationService));
        }

        machine.StationEnabledChanged += OnStationEnabledChanged;
        this.localizationService.LanguageChanged += OnLanguageChanged;

        devices.TryGet(DeviceIds.MainPlc, out mainPlc);
        if (mainPlc != null)
        {
            mainPlc.StateChanged += OnMainPlcStateChanged;
            if (mainPlc.IsConnected)
            {
                _ = RefreshFromPlcAsync();
            }
        }
    }

    public ObservableCollection<StationEnableItemModel> Items => items;

    public async Task RefreshFromPlcAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || Interlocked.Exchange(ref refreshGate, 1) == 1)
        {
            return;
        }

        try
        {
            await machine.RefreshStationEnabledStatesAsync(cancellationToken).ConfigureAwait(false);
            SyncFromStations();
        }
        finally
        {
            Volatile.Write(ref refreshGate, 0);
        }
    }

    public async Task SetStationEnabledAsync(StationEnableItemModel item, bool isEnabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await machine.SetStationEnabledAsync(item.Station, isEnabled, cancellationToken).ConfigureAwait(false);
        SyncStation(item.Station);
    }

    private void OnStationEnabledChanged(object? sender, StationEnabledChangedEventArgs e)
        => SyncStation(e.Station);

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => RunOnUi(() =>
        {
            foreach (StationEnableItemModel item in items)
            {
                item.RefreshLocalization();
            }
        });

    private void OnMainPlcStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (disposed || e.CurrentState != ConnectionState.Connected)
        {
            return;
        }

        _ = RefreshFromPlcAsync();
    }

    private void SyncFromStations()
        => RunOnUi(() =>
        {
            foreach (StationEnableItemModel item in items)
            {
                item.SyncFromStation();
            }
        });

    private void SyncStation(TestStationModel station)
        => RunOnUi(() =>
        {
            StationEnableItemModel? item = items.FirstOrDefault(value => value.StationId == station.StationId);
            item?.SyncFromStation();
        });

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        machine.StationEnabledChanged -= OnStationEnabledChanged;
        localizationService.LanguageChanged -= OnLanguageChanged;
        if (mainPlc != null)
        {
            mainPlc.StateChanged -= OnMainPlcStateChanged;
        }
    }
}