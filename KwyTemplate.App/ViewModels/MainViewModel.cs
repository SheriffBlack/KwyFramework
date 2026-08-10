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

namespace KwyTemplate.App.ViewModels;

public class MainViewModel : BindableBase, INavigationAware
{
    private readonly IRegionManager regionManager;
    private readonly MachineBase machine;
    private readonly IPermissionService? permissionService;
    private readonly IAppNotificationService? notificationService;
    private readonly ILocalizationService localizationService;
    private readonly NavigationConfigModel navigationConfig = new();
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
        this.localizationService.LanguageChanged += OnLanguageChanged;
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
                DisplayText = T("Nav.Correction", "校正"),
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
                await notificationService.WarningAsync(message, T("Main.Title.PermissionDenied", "权限不足"));
            }

            return;
        }

        if (!await EnsureCanNavigateToCompensateAsync(item, previousView, previousNavigationKey))
        {
            return;
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

    private async Task<bool> EnsureCanNavigateToCompensateAsync(
        NavigationItemModel item,
        string previousView,
        string previousNavigationKey)
    {
        if (!string.Equals(item.ViewName, ViewNames.CompensateView, StringComparison.OrdinalIgnoreCase)
            || machine.ProductionState != MachineProductionState.Running)
        {
            return true;
        }

        if (notificationService == null)
        {
            RestoreNavigationSelection(previousView, previousNavigationKey);
            return false;
        }

        bool shouldPause = await notificationService.ConfirmAsync(
            T("Main.Message.EnterCompensateNeedPause", "机台运行中，进入点检界面需要暂停，是否暂停？"),
            T("Main.Title.EnterCompensate", "进入点检"));
        if (!shouldPause)
        {
            RestoreNavigationSelection(previousView, previousNavigationKey);
            return false;
        }

        try
        {
            await machine.PauseAsync();
            return true;
        }
        catch (Exception ex)
        {
            RestoreNavigationSelection(previousView, previousNavigationKey);
            await notificationService.ErrorAsync(TF("Main.Message.PauseFailed", "机台暂停失败：\n{0}", ex.Message), T("Main.Title.EnterCompensate", "进入点检"), ex);
            return false;
        }
    }


    private string T(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private string TF(string key, string fallback, params object[] args)
        => string.Format(System.Globalization.CultureInfo.CurrentCulture, T(key, fallback), args);
    private void RestoreNavigationSelection(string previousView, string previousNavigationKey)
    {
        SelectedView = previousView;
        SelectedNavigationKey = previousNavigationKey;
        RaisePropertyChanged(nameof(SelectedView));
        RaisePropertyChanged(nameof(SelectedNavigationKey));
    }
}

