using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;

namespace KwyTemplate.App.ViewModels;

public sealed class SystemViewModel : BindableBase, INavigationAware
{
    private const string ProgramSettingsParameter = "ProgramSettings";

    private readonly IRegionManager regionManager;
    private readonly ProgramSettingsStore programSettingsStore;
    private readonly ILocalizationService localizationService;
    private readonly NavigationConfigModel navigationConfig = new();
    private bool isInitialized;
    private string selectedNavigationKey = string.Empty;
    private string selectedParameterHeader = string.Empty;
    private string statusMessage = string.Empty;
    private object? selectedParameterSource;
    private bool isParameterEditorVisible;

    public SystemViewModel(IRegionManager regionManager, ProgramSettingsStore programSettingsStore, ILocalizationService localizationService)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.programSettingsStore = programSettingsStore ?? throw new ArgumentNullException(nameof(programSettingsStore));
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
        private set => SetProperty(ref selectedParameterSource, value);
    }

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

        ClearParameterEditor();

        if (!string.IsNullOrWhiteSpace(item.ViewName))
        {
            regionManager.RequestNavigate(RegionNames.SystemRegion, item.ViewName);
        }
    }

    private void LoadProgramSettings()
    {
        IsParameterEditorVisible = true;
        SelectedParameterHeader = GetLocalizedText("Nav.ProgramSettings", "程序设定");

        try
        {
            ProgramSettingsLoadResult result = programSettingsStore.LoadOrCreate();
            SelectedParameterSource = result.Settings;
            StatusMessage = result.Created
                ? TF("System.Status.ProgramSettingsCreated", "程序设定文件不存在，已使用默认值并创建配置：{0}", result.FilePath)
                : TF("System.Status.ProgramSettingsReloaded", "程序设定已从 {0} 重载。", result.FilePath);
        }
        catch (Exception ex)
        {
            SelectedParameterSource = new ProgramSettingsModel();
            StatusMessage = TF("System.Status.ProgramSettingsReloadFailed", "程序设定重载失败，已使用默认值：{0}", ex.Message);
        }

        applyCommand?.RaiseCanExecuteChanged();
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
            SelectedParameterHeader = GetLocalizedText("Nav.ProgramSettings", "程序设定");
        }
    }

    private void RefreshNavigationLocalization()
    {
        foreach (NavigationItemModel item in NavigationItems)
        {
            item.RefreshLocalization(localizationService);
        }
    }

    private string GetLocalizedText(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private string TF(string key, string fallback, params object[] args)
        => string.Format(System.Globalization.CultureInfo.CurrentCulture, GetLocalizedText(key, fallback), args);
    private bool CanApply() => SelectedParameterSource is ProgramSettingsModel;

    private async Task ExecuteApplyAsync()
    {
        if (SelectedParameterSource is not ProgramSettingsModel settings)
        {
            StatusMessage = GetLocalizedText("System.Status.NoProgramSettingsToSave", "当前没有可保存的程序设定。");
            return;
        }

        try
        {
            await programSettingsStore.SaveAsync(settings).ConfigureAwait(false);
            StatusMessage = TF("System.Status.ProgramSettingsSaved", "程序设定已保存到 {0}", programSettingsStore.FilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = TF("System.Status.ProgramSettingsSaveFailed", "程序设定保存失败：{0}", ex.Message);
        }
    }
}




