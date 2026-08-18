using System.Globalization;
using Kwy.Device.Abstractions;
using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using Kwy.MVVM.Regions;
using Kwy.UI.WPF.Components.Dialogs;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Contracts.Security;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Profiles;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.ViewModels;

internal class SetViewModel : BindableBase, INavigationAware
{
    private const string BraidOptionsParameter = "BraidOptions";
    private const string CompensateOptionsParameter = "CompensateOptions";
    private const string MarkPrintOptionsParameter = "MarkPrintOptions";
    private const string InstrumentParameterHeader = "仪表参数";
    private const string BraidParameterHeader = "编带参数";
    private const string MarkPrintParameterHeader = "\u7F16\u5E26\u5B57\u7B26";
    private const string MesOnlineParameterLockedMessage = "MES在线时禁止编辑本地参数，请先断开MES。";
    private const string MesOnlineBraidLockedMessage = "MES在线时禁止编辑本地编带参数，请先断开MES。";
    private const string MesOnlineMarkPrintLockedMessage = "\u004D\u0045\u0053\u5728\u7EBF\u65F6\u7981\u6B62\u7F16\u8F91\u672C\u5730\u7F16\u5E26\u5B57\u7B26\uFF0C\u8BF7\u5148\u65AD\u5F00\u004D\u0045\u0053\u3002";
    private readonly IRegionManager regionManager;
    private readonly MachineBase machine;
    private readonly IMachineDeviceContext devices;
    private readonly IDeviceConfigProvider configProvider;
    private readonly BraidOptionsStore braidOptionsStore;
    private readonly CompensateOptionsStore compensateOptionsStore;
    private readonly MarkPrintOptionsStore markPrintOptionsStore;
    private readonly MesConnectionStatus mesConnectionStatus;
    private readonly IPermissionService? permissionService;

    private readonly IProductionContext productionContext;
    private readonly IMessageBus messageBus;
    private readonly IDialogMessageService dialogMessageService;
    private readonly ILocalizationService localizationService;
    private readonly IDisposable stationLimitsAppliedSubscription;
    private readonly NavigationConfigModel navigationConfig = new();
    private bool isInitialized;
    private string selectedSubView = string.Empty;
    private string selectedParameter = string.Empty;
    private string selectedNavigationKey = string.Empty;
    private string selectedParameterHeader = InstrumentParameterHeader;
    private string statusMessage = string.Empty;
    private object? selectedParameterSource;
    private IConfigurableDevice? selectedConfigurableDevice;
    private bool isParameterEditorVisible;
    private DelegateCommand<NavigationItemModel>? navigateCommand;
    private AsyncDelegateCommand? applyCommand;

    public SetViewModel(
        IRegionManager regionManager,
        MachineBase machine,
        IMachineDeviceContext devices,
        IDeviceConfigProvider configProvider,
        BraidOptionsStore braidOptionsStore,
        CompensateOptionsStore compensateOptionsStore,
        MarkPrintOptionsStore markPrintOptionsStore,
        MesConnectionStatus mesConnectionStatus,
        IProductionContext productionContext,
        IMessageBus messageBus,
        IDialogMessageService dialogMessageService,
        ILocalizationService localizationService,
        IPermissionService? permissionService = null)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.braidOptionsStore = braidOptionsStore ?? throw new ArgumentNullException(nameof(braidOptionsStore));
        this.compensateOptionsStore = compensateOptionsStore ?? throw new ArgumentNullException(nameof(compensateOptionsStore));
        this.markPrintOptionsStore = markPrintOptionsStore ?? throw new ArgumentNullException(nameof(markPrintOptionsStore));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.permissionService = permissionService;

        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        this.dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        stationLimitsAppliedSubscription = this.messageBus.Subscribe<SetViewModel, StationLimitsAppliedMessage>(
            this,
            static (viewModel, _) => viewModel.RefreshSelectedInstrumentParameter(),
            MessageSubscribeOptions<StationLimitsAppliedMessage>.OnUI);
        this.mesConnectionStatus.PropertyChanged += OnMesConnectionStatusPropertyChanged;
        this.localizationService.LanguageChanged += OnLanguageChanged;
        InitializeNavigationItems();
    }

    public List<NavigationItemModel> NavigationItems { get; private set; } = new();

    public string SelectedSubView
    {
        get => selectedSubView;
        set => SetProperty(ref selectedSubView, value);
    }

    public string SelectedParameter
    {
        get => selectedParameter;
        set => SetProperty(ref selectedParameter, value);
    }

    public string SelectedNavigationKey
    {
        get => selectedNavigationKey;
        set => SetProperty(ref selectedNavigationKey, value);
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

    public bool CanEditParameters => mesConnectionStatus.State != MesConnectionState.Online;

    public bool IsParameterEditorVisible
    {
        get => isParameterEditorVisible;
        private set => SetProperty(ref isParameterEditorVisible, value);
    }

    public DelegateCommand<NavigationItemModel> NavigateCommand => navigateCommand ??= new DelegateCommand<NavigationItemModel>(NavigateToSubView);

    public AsyncDelegateCommand ApplyCommand => applyCommand ??= new AsyncDelegateCommand(ExecuteApplyAsync, CanApply);

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
        }
        else
        {
            RaisePropertyChanged(nameof(SelectedSubView));
            RaisePropertyChanged(nameof(SelectedParameter));
            RaisePropertyChanged(nameof(SelectedNavigationKey));
            RaisePropertyChanged(nameof(CanEditParameters));
            applyCommand?.RaiseCanExecuteChanged();
        }
    }

    private void InitializeNavigationItems()
    {
        var items = new List<NavigationItemModel>();
        items.AddRange(CreateStationInstrumentItems());
        items.Add(new NavigationItemModel
        {
            DisplayText = BraidParameterHeader,
            LocalizationKey = "Nav.BraidOptions",
            Parameter = BraidOptionsParameter
        });
        if (machine is IMachineMarkPrintOptionsMachine)
        {
            items.Add(new NavigationItemModel
            {
                DisplayText = MarkPrintParameterHeader,
                LocalizationKey = "Nav.MarkPrintOptions",
                Parameter = MarkPrintOptionsParameter
            });
        }

        items.Add(new NavigationItemModel
        {
            DisplayText = "点检配置",
            LocalizationKey = "Nav.CompensateOptions",
            Parameter = CompensateOptionsParameter
        });

        if (navigationConfig.SecondaryNavigationItems.TryGetValue(ViewNames.SetView, out List<NavigationItemModel>? staticItems))
        {
            items.AddRange(staticItems.Where(static item => item.IsVisibility));
        }

        foreach (NavigationItemModel navigationItem in items)
        {
            navigationItem.RefreshLocalization(localizationService);
        }

        NavigationItems = items;
    }

    private IEnumerable<NavigationItemModel> CreateStationInstrumentItems()
    {
        var usedDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (TestStationModel station in machine.Stations.OrderBy(static item => item.StationId))
        {
            foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                if (!usedDeviceIds.Add(deviceId))
                {
                    continue;
                }

                yield return new NavigationItemModel
                {
                    DisplayText = localizationService.TF("Set.Nav.StationInstrumentParameter", "{0} 参数", ResolveStationShortName(station)),
                    Parameter = deviceId
                };
            }
        }
    }

    private async void NavigateToSubView(NavigationItemModel? item)
    {
        if (item == null)
        {
            return;
        }

        string previousNavigationKey = SelectedNavigationKey;
        if (!string.IsNullOrWhiteSpace(item.PermissionCode)
            && permissionService?.HasPermission(item.PermissionCode) == false)
        {
            SelectedNavigationKey = previousNavigationKey;
            RaisePropertyChanged(nameof(SelectedNavigationKey));

            string message = permissionService.GetNoPermissionMessage(item.PermissionCode);
            await dialogMessageService.ShowWarningAsync(message, localizationService.T("Main.Title.PermissionDenied", "权限不足")).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.PermissionCode))
        {
            (permissionService as IPermissionUsageNotifier)?.NotifyPermissionUsed(item.PermissionCode);
        }

        SelectedSubView = item.ViewName;
        SelectedParameter = item.Parameter;
        SelectedNavigationKey = item.NavigationKey;

        if (string.IsNullOrWhiteSpace(item.ViewName) && !string.IsNullOrWhiteSpace(item.Parameter))
        {
            if (string.Equals(item.Parameter, BraidOptionsParameter, StringComparison.OrdinalIgnoreCase))
            {
                LoadBraidOptions();
                return;
            }

            if (string.Equals(item.Parameter, MarkPrintOptionsParameter, StringComparison.OrdinalIgnoreCase))
            {
                LoadMarkPrintOptions();
                return;
            }

            if (string.Equals(item.Parameter, CompensateOptionsParameter, StringComparison.OrdinalIgnoreCase))
            {
                LoadCompensateOptions();
                return;
            }

            LoadInstrumentParameter(item.Parameter);
            return;
        }

        ClearInstrumentParameter();

        if (string.IsNullOrWhiteSpace(item.ViewName))
        {
            return;
        }

        var parameters = new NavigationParameters();
        if (!string.IsNullOrWhiteSpace(item.Parameter))
        {
            parameters.Add(NavigationParameterKeys.Parameter, item.Parameter);
        }

        regionManager.RequestNavigate(RegionNames.SetRegion, item.ViewName, parameters);
    }

    private void LoadInstrumentParameter(string deviceId)
    {
        selectedConfigurableDevice = null;
        SelectedParameterSource = null;
        IsParameterEditorVisible = true;

        if (!devices.TryGet(deviceId, out IConfigurableDevice? device) || device == null)
        {
            SelectedParameterHeader = deviceId;
            StatusMessage = localizationService.TF("Set.Status.DeviceNotRegistered", "设备未注册：{0}", deviceId);
            applyCommand?.RaiseCanExecuteChanged();
            return;
        }

        selectedConfigurableDevice = device;
        SelectedParameterSource = device.DeviceParameter;
        SelectedParameterHeader = device is IDevice kwyDevice ? kwyDevice.DeviceName : deviceId;
        StatusMessage = CanEditParameters
            ? localizationService.T("Set.Status.InstrumentEditHint", "修改参数后点击应用保存；若设备已连接，部分参数需要重新初始化仪表后生效。")
            : localizationService.T("Set.Status.MesOnlineParameterLocked", MesOnlineParameterLockedMessage);
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshSelectedInstrumentParameter()
    {
        if (selectedConfigurableDevice == null)
        {
            return;
        }

        // HIOKI 配置对象不实现 INotifyPropertyChanged。重新绑定当前对象，
        // 使已打开的 SetView 工位参数立即显示 MES/工单运行时更新后的限值。
        SelectedParameterSource = null;
        SelectedParameterSource = selectedConfigurableDevice.DeviceParameter;
    }

    private void LoadBraidOptions()
    {
        selectedConfigurableDevice = null;
        SelectedParameterSource = null;
        IsParameterEditorVisible = true;

        BraidOptionsLoadResult result = braidOptionsStore.LoadOrCreate();
        SelectedParameterSource = result.Options;
        SelectedParameterHeader = localizationService.T("Nav.BraidOptions", BraidParameterHeader);
        StatusMessage = CanEditParameters
            ? result.Created
                ? localizationService.TF("Set.Status.BraidCreated", "已创建默认编带参数：{0}", result.FilePath)
                : localizationService.T("Set.Status.BraidEditHint", "修改编带参数后点击应用保存；MES离线时会按本地值写入PLC。")
            : localizationService.T("Set.Status.MesOnlineBraidLocked", MesOnlineBraidLockedMessage);
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void LoadMarkPrintOptions()
    {
        selectedConfigurableDevice = null;
        SelectedParameterSource = null;
        IsParameterEditorVisible = true;

        MarkPrintOptionsLoadResult result = markPrintOptionsStore.LoadOrCreate();
        SelectedParameterSource = result.Options;
        SelectedParameterHeader = localizationService.T("Nav.MarkPrintOptions", MarkPrintParameterHeader);
        StatusMessage = CanEditParameters
            ? result.Created
                ? localizationService.TF("Set.Status.MarkPrintCreated", "\u5DF2\u521B\u5EFA\u9ED8\u8BA4\u7F16\u5E26\u5B57\u7B26\uFF1A{0}", result.FilePath)
                : localizationService.T("Set.Status.MarkPrintEditHint", "\u4FEE\u6539\u7F16\u5E26\u5B57\u7B26\u540E\u70B9\u51FB\u5E94\u7528\u4FDD\u5B58\u3002")
            : localizationService.T("Set.Status.MesOnlineMarkPrintLocked", MesOnlineMarkPrintLockedMessage);
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void LoadCompensateOptions()
    {
        selectedConfigurableDevice = null;
        SelectedParameterSource = null;
        IsParameterEditorVisible = true;

        CompensateOptionsLoadResult result = compensateOptionsStore.LoadOrCreate();
        SelectedParameterSource = result.Options;
        SelectedParameterHeader = localizationService.T("Nav.CompensateOptions", "点检配置");
        StatusMessage = CanEditParameters
            ? result.Created
                ? localizationService.TF("Set.Status.CompensateCreated", "已创建默认点检配置：{0}", result.FilePath)
                : localizationService.T("Set.Status.CompensateEditHint", "修改点检配置后点击应用保存。")
            : localizationService.T("Set.Status.MesOnlineCompensateLocked", "MES在线时禁止编辑本地点检配置，请先断开MES。");
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void ClearInstrumentParameter()
    {
        selectedConfigurableDevice = null;
        SelectedParameterSource = null;
        IsParameterEditorVisible = false;
        StatusMessage = string.Empty;
        applyCommand?.RaiseCanExecuteChanged();
    }

    private bool CanApply() => CanEditParameters && (selectedConfigurableDevice != null || SelectedParameterSource is BraidOptions or CompensateOptions or MarkPrintOptions);

    private async Task ExecuteApplyAsync()
    {
        try
        {
            if (!CanEditParameters)
            {
                StatusMessage = localizationService.T("Set.Status.MesOnlineApplyLocked", "MES在线时禁止应用本地参数，请先断开MES。");
                return;
            }

            if (SelectedParameterSource is BraidOptions braidOptions)
            {
                await braidOptionsStore.SaveAsync(braidOptions).ConfigureAwait(true);
                if (machine is IMachineBraidSetupMachine braidMachine)
                {
                    await braidMachine.ApplyBraidSetupAsync(braidOptions.ToTapeSetup(), DestroyToken).ConfigureAwait(true);
                    StatusMessage = localizationService.T("Set.Status.BraidSavedAndWritten", "编带参数已保存并写入PLC。");
                    return;
                }

                StatusMessage = localizationService.T("Set.Status.BraidSavedUnsupported", "编带参数已保存，当前机型不支持写入PLC。");
                return;
            }

            if (SelectedParameterSource is MarkPrintOptions markPrintOptions)
            {
                await markPrintOptionsStore.SaveAsync(markPrintOptions).ConfigureAwait(true);
                if (machine is IMachineMarkPrintOptionsMachine markPrintMachine)
                {
                    await markPrintMachine.ApplyMarkPrintStringAsync(markPrintOptions.PrintString, DestroyToken).ConfigureAwait(true);
                }

                StatusMessage = localizationService.T("Set.Status.MarkPrintSaved", "编带字符已保存。");
                return;
            }

            if (SelectedParameterSource is CompensateOptions compensateOptions)
            {
                await compensateOptionsStore.SaveAsync(compensateOptions).ConfigureAwait(true);
                messageBus.Publish(new CompensateOptionsChangedMessage(compensateOptions));
                StatusMessage = localizationService.T("Set.Status.CompensateSaved", "点检配置已保存。");
                return;
            }

            if (selectedConfigurableDevice == null)
            {
                StatusMessage = localizationService.T("Set.Status.NoParameterToSave", "当前没有可保存的参数。");
                return;
            }

            if (!selectedConfigurableDevice.DeviceParameter.Validate())
            {
                StatusMessage = localizationService.T("Set.Status.ParameterValidateFailed", "参数校验失败，请检查上下限、量程等配置。");
                return;
            }

            await selectedConfigurableDevice.ApplyConfigAsync(DestroyToken).ConfigureAwait(true);
            await configProvider.SaveAsync(DestroyToken).ConfigureAwait(true);
            machine.RefreshStationLimitsFromInstrumentConfigs();

            productionContext.IsResultGridDataEnabled = true;
            messageBus.Publish(new StationLimitsAppliedMessage());
            StatusMessage = localizationService.T("Set.Status.InstrumentSaved", "仪表参数已保存。");
        }
        catch (Exception ex)
        {
            string targetName = selectedConfigurableDevice is IDevice device ? device.DeviceName : SelectedParameterHeader;
            string message = localizationService.TF("Set.Message.ApplyFailed", "{0}参数应用失败！\n{1}", targetName, ex.Message);
            StatusMessage = message;
            await dialogMessageService.ShowErrorAsync(message, localizationService.T("Set.Title.ApplyFailed", "参数应用失败")).ConfigureAwait(true);
        }
    }

    private void OnMesConnectionStatusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(MesConnectionStatus.State), StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        RaisePropertyChanged(nameof(CanEditParameters));
        RefreshMesLockStatusMessage();
        applyCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshMesLockStatusMessage()
    {
        if (!IsParameterEditorVisible || SelectedParameterSource == null)
        {
            return;
        }

        StatusMessage = SelectedParameterSource switch
        {
            BraidOptions when !CanEditParameters => localizationService.T("Set.Status.MesOnlineBraidLocked", MesOnlineBraidLockedMessage),
            BraidOptions => localizationService.T("Set.Status.BraidEditHint", "\u4FEE\u6539\u7F16\u5E26\u53C2\u6570\u540E\u70B9\u51FB\u5E94\u7528\u4FDD\u5B58\uFF1BMES\u79BB\u7EBF\u65F6\u4F1A\u6309\u672C\u5730\u503C\u5199\u5165PLC\u3002"),
            CompensateOptions when !CanEditParameters => localizationService.T("Set.Status.MesOnlineCompensateLocked", "MES在线时禁止编辑本地点检配置，请先断开MES。"),
            CompensateOptions => localizationService.T("Set.Status.CompensateEditHint", "修改点检配置后点击应用保存。"),
            MarkPrintOptions when !CanEditParameters => localizationService.T("Set.Status.MesOnlineMarkPrintLocked", MesOnlineMarkPrintLockedMessage),
            MarkPrintOptions => localizationService.T("Set.Status.MarkPrintEditHint", "\u4FEE\u6539\u7F16\u5E26\u5B57\u7B26\u540E\u70B9\u51FB\u5E94\u7528\u4FDD\u5B58\u3002"),
            _ when !CanEditParameters => localizationService.T("Set.Status.MesOnlineParameterLocked", MesOnlineParameterLockedMessage),
            _ => localizationService.T("Set.Status.InstrumentEditHint", "\u4FEE\u6539\u53C2\u6570\u540E\u70B9\u51FB\u5E94\u7528\u4FDD\u5B58\uFF1B\u82E5\u8BBE\u5907\u5DF2\u8FDE\u63A5\uFF0C\u90E8\u5206\u53C2\u6570\u9700\u8981\u91CD\u65B0\u521D\u59CB\u5316\u4EEA\u8868\u540E\u751F\u6548\u3002")
        };
    }

    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        foreach (NavigationItemModel item in NavigationItems)
        {
            item.RefreshLocalization(localizationService);
        }

        RefreshStationInstrumentNavigationTexts();
        RefreshMesLockStatusMessage();

        if (SelectedParameterSource is BraidOptions)
        {
            SelectedParameterHeader = localizationService.T("Nav.BraidOptions", BraidParameterHeader);
        }
        else if (SelectedParameterSource is CompensateOptions)
        {
            SelectedParameterHeader = localizationService.T("Nav.CompensateOptions", "点检配置");
        }
        else if (SelectedParameterSource is MarkPrintOptions)
        {
            SelectedParameterHeader = localizationService.T("Nav.MarkPrintOptions", MarkPrintParameterHeader);
        }

    }

    private void RefreshStationInstrumentNavigationTexts()
    {
        var usedDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (TestStationModel station in machine.Stations.OrderBy(static item => item.StationId))
        {
            foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                if (!usedDeviceIds.Add(deviceId))
                {
                    continue;
                }

                NavigationItemModel? item = NavigationItems.FirstOrDefault(candidate => string.Equals(candidate.Parameter, deviceId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.DisplayText = localizationService.TF("Set.Nav.StationInstrumentParameter", "{0} 参数", ResolveStationShortName(station));
                }
            }
        }
    }

    private string ResolveStationShortName(TestStationModel station)
    {
        if (!string.IsNullOrWhiteSpace(station.StationShortNameKey))
        {
            return localizationService.T(station.StationShortNameKey, station.StationName);
        }

        return string.IsNullOrWhiteSpace(station.StationNameKey)
            ? station.StationName
            : localizationService.T(station.StationNameKey, station.StationName);
    }

}


