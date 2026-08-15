using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using Kwy.MVVM.WPF.Regions;
using Kwy.UI;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using System.Windows.Threading;

namespace KwyTemplate.App.ViewModels;

public class MainViewModel : BindableBase, INavigationAware
{
    private readonly IRegionManager regionManager;
    private readonly MachineBase machine;
    private readonly IPermissionService? permissionService;
    private readonly IAppNotificationService? notificationService;
    private readonly ILocalizationService localizationService;
    private readonly NavigationConfigModel navigationConfig = new();
    private readonly Dispatcher dispatcher;
    private bool isInitialized;
    private string selectedView = string.Empty;
    private string selectedNavigationKey = string.Empty;

    public MainViewModel(
        IRegionManager regionManager,
        MachineBase machine,
        ILocalizationService localizationService,
        IPermissionService? permissionService = null,
        IAppNotificationService? notificationService = null)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.permissionService = permissionService;
        this.notificationService = notificationService;
        dispatcher = Dispatcher.CurrentDispatcher;
        this.localizationService.LanguageChanged += OnLanguageChanged;
        machine.RunningStateChanged += OnMachineRunningStateChanged;
        InitializeNavigationItems();
    }

    public List<NavigationItemModel> NavigationItems { get; private set; } = new();

    public string SelectedView
    {
        get => selectedView;
        set => SetProperty(ref selectedView, value);
    }

    public string SelectedNavigationKey
    {
        get => selectedNavigationKey;
        set => SetProperty(ref selectedNavigationKey, value);
    }

    private DelegateCommand<NavigationItemModel>? navigateCommand;
    public DelegateCommand<NavigationItemModel> NavigateCommand => navigateCommand ??= new DelegateCommand<NavigationItemModel>(NavigateToView);

    private void InitializeNavigationItems()
    {
        List<NavigationItemModel> items = navigationConfig.PrimaryNavigationItems
            .Where(x => x.IsVisibility)
            .ToList();

        if (HasStationCalibrationOperation() && items.All(item => item.ViewName != ViewNames.CorrectionView))
        {
            var correctionItem = new NavigationItemModel
            {
                ViewName = ViewNames.CorrectionView,
                DisplayText = localizationService.T("Nav.Correction", "校正"),
                LocalizationKey = "Nav.Correction",
                Icon = IconNames.IconCorrection
            };

            int compensateIndex = items.FindIndex(item => item.ViewName == ViewNames.CompensateView);
            if (compensateIndex >= 0)
            {
                items.Insert(compensateIndex, correctionItem);
            }
            else
            {
                items.Add(correctionItem);
            }
        }

        foreach (NavigationItemModel item in items)
        {
            item.RefreshLocalization(localizationService);
        }

        NavigationItems = items;
        RefreshNavigationAvailability();
    }

    private void OnMachineRunningStateChanged(object? sender, EventArgs e)
    {
        if (dispatcher.CheckAccess())
        {
            RefreshNavigationAvailability();
            return;
        }

        _ = dispatcher.InvokeAsync(RefreshNavigationAvailability);
    }

    private void RefreshNavigationAvailability()
    {
        bool isProductionRunning = machine.ProductionState == MachineProductionState.Running;
        foreach (NavigationItemModel item in NavigationItems)
        {
            item.IsNavigationEnabled = !isProductionRunning
                || string.Equals(item.ViewName, ViewNames.HomeView, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        foreach (NavigationItemModel item in NavigationItems)
        {
            item.RefreshLocalization(localizationService);
        }
    }

    private bool HasStationCalibrationOperation()
        => machine.TestStations.Any(station => station.Operations.Any(operation =>
            string.Equals(operation.Code, StationOperationDescriptor.Calibration, StringComparison.OrdinalIgnoreCase)));

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (!isInitialized)
        {
            isInitialized = true;
            NavigationItemModel? defaultItem = NavigationItems.FirstOrDefault(x => x.ViewName == ViewNames.HomeView)
                ?? NavigationItems.FirstOrDefault();
            NavigateToView(defaultItem);
        }
        else
        {
            RaisePropertyChanged(nameof(SelectedView));
            RaisePropertyChanged(nameof(SelectedNavigationKey));
        }
    }

    private async void NavigateToView(NavigationItemModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ViewName))
        {
            return;
        }

        string previousView = SelectedView;
        string previousNavigationKey = SelectedNavigationKey;
        if (!string.IsNullOrWhiteSpace(item.PermissionCode)
            && permissionService?.HasPermission(item.PermissionCode) == false)
        {
            RestoreNavigationSelection(previousView, previousNavigationKey);

            if (notificationService != null)
            {
                string message = permissionService.GetNoPermissionMessage(item.PermissionCode);
                await notificationService.WarningAsync(message, localizationService.T("Main.Title.PermissionDenied", "权限不足"));
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(item.PermissionCode))
        {
            (permissionService as IPermissionUsageNotifier)?.NotifyPermissionUsed(item.PermissionCode);
        }

        SelectedView = item.ViewName;
        SelectedNavigationKey = item.NavigationKey;

        var parameters = new NavigationParameters();
        if (!string.IsNullOrWhiteSpace(item.Parameter))
        {
            parameters.Add(NavigationParameterKeys.Parameter, item.Parameter);
        }

        regionManager.RequestNavigate(RegionNames.MainRegion, item.ViewName, parameters);
    }

    private void RestoreNavigationSelection(string previousView, string previousNavigationKey)
    {
        SelectedView = previousView;
        SelectedNavigationKey = previousNavigationKey;
        RaisePropertyChanged(nameof(SelectedView));
        RaisePropertyChanged(nameof(SelectedNavigationKey));
    }
}

