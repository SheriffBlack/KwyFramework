using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Device.Profiles;

namespace KwyTemplate.App.ViewModels;

public sealed class SystemViewModel : BindableBase, INavigationAware
{
    private const string ProgramSettingsParameter = "ProgramSettings";
    private const string MachineProfileBasicParameter = "MachineProfile.Basic";
    private const string MachineProfileIoPointsParameter = "MachineProfile.IoPoints";
    private const string MachineProfilePlcPointsParameter = "MachineProfile.PlcPoints";
    private const string MachineProfileStationParameterPrefix = "MachineProfile.Station.";

    private readonly IRegionManager regionManager;
    private readonly ProgramSettingsStore programSettingsStore;
    private readonly MachineProfileEditorStore machineProfileEditorStore;
    private readonly ILocalizationService localizationService;
    private readonly NavigationConfigModel navigationConfig = new();
    private bool isInitialized;
    private string selectedNavigationKey = string.Empty;
    private string selectedParameterHeader = string.Empty;
    private string statusMessage = string.Empty;
    private object? selectedParameterSource;
    private bool isParameterEditorVisible;
    private MachineProfileEditorSession? machineProfileSession;

    public SystemViewModel(
        IRegionManager regionManager,
        ProgramSettingsStore programSettingsStore,
        MachineProfileEditorStore machineProfileEditorStore,
        ILocalizationService localizationService)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.programSettingsStore = programSettingsStore ?? throw new ArgumentNullException(nameof(programSettingsStore));
        this.machineProfileEditorStore = machineProfileEditorStore ?? throw new ArgumentNullException(nameof(machineProfileEditorStore));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.localizationService.LanguageChanged += OnLanguageChanged;
        InitializeNavigationItems();
    }

    public List<NavigationItemModel> NavigationItems { get; private set; } = new();

    public string SelectedNavigationKey
    {
        get => selectedNavigationKey;
        private set => SetProperty(ref selectedNavigationKey, value);
    }

    public string SelectedParameterHeader
    {
        get => selectedParameterHeader;
        private set => SetProperty(ref selectedParameterHeader, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public object? SelectedParameterSource
    {
        get => selectedParameterSource;
        private set
        {
            if (SetProperty(ref selectedParameterSource, value))
            {
                RaisePropertyChanged(nameof(IsPointEditorVisible));
            }
        }
    }

    public bool IsPointEditorVisible => SelectedParameterSource is MachineIoPointsEditorModel or MachinePlcPointsEditorModel;

    public bool IsParameterEditorVisible
    {
        get => isParameterEditorVisible;
        private set => SetProperty(ref isParameterEditorVisible, value);
    }

    private DelegateCommand<NavigationItemModel>? navigateCommand;

    public DelegateCommand<NavigationItemModel> NavigateCommand => navigateCommand ??= new DelegateCommand<NavigationItemModel>(NavigateToSubView);

    private AsyncDelegateCommand? applyCommand;

    public AsyncDelegateCommand ApplyCommand => applyCommand ??= new AsyncDelegateCommand(ExecuteApplyAsync, CanApply);

    private void InitializeNavigationItems()
    {
        if (navigationConfig.SecondaryNavigationItems.TryGetValue(ViewNames.SystemView, out List<NavigationItemModel>? staticItems))
        {
            NavigationItems = staticItems.Where(static item => item.IsVisibility).ToList();
            RefreshNavigationLocalization();
        }
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (!isInitialized)
        {
            isInitialized = true;
            EnsureMachineProfileSession();
            NavigateToSubView(NavigationItems.FirstOrDefault());
            return;
        }

        RaisePropertyChanged(nameof(SelectedNavigationKey));
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void NavigateToSubView(NavigationItemModel? item)
    {
        if (item == null)
        {
            return;
        }

        SelectedNavigationKey = item.NavigationKey;

        if (string.IsNullOrWhiteSpace(item.ViewName) && string.Equals(item.Parameter, ProgramSettingsParameter, StringComparison.OrdinalIgnoreCase))
        {
            LoadProgramSettings();
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ViewName) && item.Parameter.StartsWith("MachineProfile.", StringComparison.OrdinalIgnoreCase))
        {
            LoadMachineProfileEditor(item.Parameter);
            return;
        }

        ClearParameterEditor();

        if (!string.IsNullOrWhiteSpace(item.ViewName))
        {
            regionManager.RequestNavigate(RegionNames.SystemRegion, item.ViewName);
        }
    }

    private void LoadProgramSettings()
    {
        IsParameterEditorVisible = true;
        SelectedParameterHeader = localizationService.T("Nav.ProgramSettings", "程序设定");

        try
        {
            ProgramSettingsLoadResult result = programSettingsStore.LoadOrCreate();
            SelectedParameterSource = result.Settings;
            StatusMessage = result.Created
                ? localizationService.TF("System.Status.ProgramSettingsCreated", "程序设定文件不存在，已使用默认值并创建配置：{0}", result.FilePath)
                : localizationService.TF("System.Status.ProgramSettingsReloaded", "程序设定已从 {0} 重载。", result.FilePath);
        }
        catch (Exception ex)
        {
            SelectedParameterSource = new ProgramSettingsModel();
            StatusMessage = localizationService.TF("System.Status.ProgramSettingsReloadFailed", "程序设定重载失败，已使用默认值：{0}", ex.Message);
        }

        applyCommand?.RaiseCanExecuteChanged();
    }

    private void LoadMachineProfileEditor(string parameter)
    {
        try
        {
            machineProfileSession ??= CreateMachineProfileSession();
            IsParameterEditorVisible = true;
            if (string.Equals(parameter, MachineProfileBasicParameter, StringComparison.OrdinalIgnoreCase))
            {
                SelectedParameterHeader = "机种配置";
                SelectedParameterSource = machineProfileSession.Basic;
            }
            else if (string.Equals(parameter, MachineProfileIoPointsParameter, StringComparison.OrdinalIgnoreCase))
            {
                SelectedParameterHeader = "IO 点位设定";
                SelectedParameterSource = machineProfileSession.IoPoints;
            }
            else if (string.Equals(parameter, MachineProfilePlcPointsParameter, StringComparison.OrdinalIgnoreCase))
            {
                SelectedParameterHeader = "PLC 点位设定";
                SelectedParameterSource = machineProfileSession.PlcPoints;
            }
            else if (int.TryParse(parameter[MachineProfileStationParameterPrefix.Length..], out int stationId))
            {
                MachineStationEditorModel? station = machineProfileSession.Stations.FirstOrDefault(item => item.StationId == stationId);
                if (station == null)
                {
                    throw new InvalidOperationException($"未找到工位 {stationId}。 ");
                }

                SelectedParameterHeader = $"工位 {stationId} 设定";
                SelectedParameterSource = station;
            }

            StatusMessage = $"配置保存到 {machineProfileEditorStore.GetFilePath(machineProfileSession.Profile.ProfileKey)}，重启程序后生效。";
        }
        catch (Exception ex)
        {
            ClearParameterEditor();
            StatusMessage = $"机种配置加载失败：{ex.Message}";
        }

        applyCommand?.RaiseCanExecuteChanged();
    }

    private MachineProfileEditorSession CreateMachineProfileSession(string? profileKey = null, MachineRuntimeOptions? runtimeOptions = null)
    {
        MachineRuntimeOptions options = runtimeOptions ?? machineProfileEditorStore.LoadRuntimeOptions();
        var session = new MachineProfileEditorSession(
            string.IsNullOrWhiteSpace(profileKey) ? machineProfileEditorStore.LoadOrCreate() : machineProfileEditorStore.LoadOrCreate(profileKey),
            options,
            machineProfileEditorStore.GetConfigurableProfileKeys,
            SelectConfigurableProfile);
        session.StructureChanged += (_, _) => RebuildMachineProfileNavigation(session);
        RebuildMachineProfileNavigation(session);
        return session;
    }

    private void SelectConfigurableProfile(string profileKey)
    {
        if (machineProfileSession == null || string.IsNullOrWhiteSpace(profileKey))
        {
            return;
        }

        MachineRuntimeOptions options = machineProfileSession.RuntimeOptions;
        options.ActiveMachineKey = MachineRuntimeOptions.ConfigurableMachineKey;
        options.ActiveProfileKey = profileKey;
        machineProfileSession = CreateMachineProfileSession(profileKey, options);
        IsParameterEditorVisible = true;
        SelectedParameterHeader = "机种配置";
        SelectedParameterSource = machineProfileSession.Basic;
        StatusMessage = $"已切换到配置化机型 {profileKey}；点击应用后重启程序生效。";
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void EnsureMachineProfileSession()
    {
        try
        {
            machineProfileSession ??= CreateMachineProfileSession();
        }
        catch
        {
            // Keep SystemView usable for connection settings even when the optional profile is malformed.
        }
    }

    private void RebuildMachineProfileNavigation(MachineProfileEditorSession session)
    {
        // Replace the collection reference instead of mutating the existing List.
        // ItemsControl does not observe changes made directly to List<T>.
        var items = NavigationItems
            .Where(item => !item.Parameter.StartsWith(MachineProfileStationParameterPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        int basicIndex = items.FindIndex(item => string.Equals(item.Parameter, MachineProfileBasicParameter, StringComparison.OrdinalIgnoreCase));
        int insertIndex = basicIndex < 0 ? items.Count : basicIndex + 1;
        foreach (MachineStationEditorModel station in session.Stations)
        {
            items.Insert(insertIndex++, new NavigationItemModel
            {
                DisplayText = $"工位 {station.StationId} 设定",
                Parameter = $"{MachineProfileStationParameterPrefix}{station.StationId}"
            });
        }

        NavigationItems = items;
        RaisePropertyChanged(nameof(NavigationItems));
    }

    private void ClearParameterEditor()
    {
        SelectedParameterSource = null;
        SelectedParameterHeader = string.Empty;
        StatusMessage = string.Empty;
        IsParameterEditorVisible = false;
        applyCommand?.RaiseCanExecuteChanged();
    }


    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        RefreshNavigationLocalization();
        if (SelectedParameterSource is ProgramSettingsModel)
        {
            SelectedParameterHeader = localizationService.T("Nav.ProgramSettings", "程序设定");
        }
    }

    private void RefreshNavigationLocalization()
    {
        foreach (NavigationItemModel item in NavigationItems)
        {
            item.RefreshLocalization(localizationService);
        }
    }

    private bool CanApply() => SelectedParameterSource is ProgramSettingsModel or MachineBasicEditorModel or MachineStationEditorModel or MachineIoPointsEditorModel or MachinePlcPointsEditorModel;

    private async Task ExecuteApplyAsync()
    {
        if (SelectedParameterSource is MachineBasicEditorModel or MachineStationEditorModel or MachineIoPointsEditorModel or MachinePlcPointsEditorModel)
        {
            if (machineProfileSession == null)
            {
                return;
            }

            try
            {
                MachineProfileValidator.Validate(machineProfileSession.Profile);
                if (string.Equals(machineProfileSession.RuntimeOptions.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase))
                {
                    machineProfileSession.RuntimeOptions.ActiveProfileKey = machineProfileSession.Profile.ProfileKey;
                }
                await machineProfileEditorStore.SaveAsync(machineProfileSession.Profile).ConfigureAwait(false);
                await machineProfileEditorStore.SaveRuntimeOptionsAsync(machineProfileSession.RuntimeOptions).ConfigureAwait(false);
                StatusMessage = $"机种与运行选择已保存。请重启程序后生效。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"机种配置保存失败：{ex.Message}";
            }

            return;
        }

        if (SelectedParameterSource is not ProgramSettingsModel settings)
        {
            StatusMessage = localizationService.T("System.Status.NoProgramSettingsToSave", "当前没有可保存的程序设定。");
            return;
        }

        try
        {
            await programSettingsStore.SaveAsync(settings).ConfigureAwait(false);
            StatusMessage = localizationService.TF("System.Status.ProgramSettingsSaved", "程序设定已保存到 {0}", programSettingsStore.FilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = localizationService.TF("System.Status.ProgramSettingsSaveFailed", "程序设定保存失败：{0}", ex.Message);
        }
    }
}




