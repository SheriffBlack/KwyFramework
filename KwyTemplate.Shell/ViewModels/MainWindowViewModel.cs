using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions;
using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using Kwy.MVVM.Regions;
using Kwy.UI;
using Kwy.UI.WPF.Components.Dialogs;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Device;
using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Identity;
using KwyTemplate.Shell.Models;
using System.Diagnostics;
using System.Windows.Media;

namespace KwyTemplate.Shell.ViewModels;

public class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager regionManager;
    private readonly IDialogMessageService dialogMessageService;
    private readonly IAuthenticationDialogService authenticationDialogService;
    private readonly ICurrentUserService currentUserService;
    private readonly IDeviceRegistry deviceRegistry;
    private readonly IMessageDispatcher messageDispatcher;
    private IPlcDevice? subscribedMainPlc;

    private TimeSpan lastTotalProcessorTime;
    private DateTime lastCpuCheckTime;
    private float lastMemoryMB;
    private int lastRenderedSecond = -1;

    public MainWindowViewModel(
        IRegionManager regionManager,
        IDialogMessageService dialogMessageService,
        IAuthenticationDialogService authenticationDialogService,
        ICurrentUserService currentUserService,
        IDeviceRegistry deviceRegistry,
        IMessageDispatcher messageDispatcher)
    {
        this.regionManager = regionManager;
        this.dialogMessageService = dialogMessageService;
        this.authenticationDialogService = authenticationDialogService;
        this.currentUserService = currentUserService;
        this.deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        this.messageDispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
        this.currentUserService.CurrentUserChanged += OnCurrentUserChanged;


        UpdateCurrentTime();
        UpdateCurrentUserDisplayName();
        UpdateMainPlcConnectionState();

        // 默认简体中文
        SelectedLanguage = Languages.FirstOrDefault();

        using (var process = Process.GetCurrentProcess())
        {
            lastTotalProcessorTime = process.TotalProcessorTime;
            lastCpuCheckTime = DateTime.UtcNow;
        }

        CompositionTarget.Rendering += OnRendering;
        _ = Task.Run(RunStatusAsync);
    }

    #region Properties

    private float occupyMemory;

    public float OccupyMemory
    {
        get { return occupyMemory; }
        private set { SetProperty(ref occupyMemory, value); }
    }

    private float occupyCPU;

    public float OccupyCPU
    {
        get { return occupyCPU; }
        private set { SetProperty(ref occupyCPU, value); }
    }

    private string currentTime = string.Empty;

    public string CurrentTime
    {
        get { return currentTime; }
        private set { SetProperty(ref currentTime, value); }
    }

    private string currentUserDisplayName = "Operator";

    public string CurrentUserDisplayName
    {
        get { return currentUserDisplayName; }
        private set { SetProperty(ref currentUserDisplayName, value); }
    }

    private bool isMainPlcConnected;

    public bool IsMainPlcConnected
    {
        get { return isMainPlcConnected; }
        private set { SetProperty(ref isMainPlcConnected, value); }
    }

    private string mainPlcConnectionState = "Disconnected";

    public string MainPlcConnectionState
    {
        get { return mainPlcConnectionState; }
        private set { SetProperty(ref mainPlcConnectionState, value); }
    }


    public IReadOnlyList<LanguageModel> Languages { get; } = new List<LanguageModel>
    {
        new() { Icon = IconNames.IconSimplified, LanguageType = LanguageType.ZH_CN },
        new() { Icon = IconNames.IconTraditional, LanguageType = LanguageType.ZH_TW },
        new() { Icon = IconNames.IconEnglish, LanguageType = LanguageType.EN_US },
    };

    private LanguageModel? selectedLanguage;

    public LanguageModel? SelectedLanguage
    {
        get { return selectedLanguage; }
        set { SetProperty(ref selectedLanguage, value, OnSelectedLanguageChanged); }
    }

    #endregion Properties

    #region Command

    #region Initialize Navigation Command

    private DelegateCommand? initCommand;

    public DelegateCommand InitCommand => initCommand ??= new DelegateCommand(ExcuteInitCommand);

    private void ExcuteInitCommand()
    {
        regionManager.RequestNavigate(RegionNames.WindowRegion, ViewNames.MainView);
    }

    #endregion Initialize Navigation Command
    private DelegateCommand? switchUserCommand;

    public DelegateCommand SwitchUserCommand
        => switchUserCommand ??= new DelegateCommand(async () => await ExecuteSwitchUserAsync());

    private async Task ExecuteSwitchUserAsync()
    {
        CurrentUser? user = await authenticationDialogService.ShowLoginAsync(DestroyToken);
        if (user != null)
        {
            await dialogMessageService.ShowInfoAsync($"当前用户：{user.DisplayName}", "登录成功");
        }
    }

    #endregion

    private async Task RunStatusAsync()
    {
        CancellationToken cancellationToken = DestroyToken;

        while (!cancellationToken.IsCancellationRequested)
        {
            StatusSnapshot snapshot = CaptureStatusSnapshot();
            RunOnUi(() =>
            {
                ApplyOccupyMemory(snapshot.MemoryMB);
                ApplyOccupyCPU(snapshot.CpuPercent);
                UpdateCurrentUserDisplayName();
                UpdateMainPlcConnectionState();
            });

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void UpdateCurrentTime()
    {
        ApplyCurrentTime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        if (now.Second == lastRenderedSecond)
        {
            return;
        }

        lastRenderedSecond = now.Second;
        ApplyCurrentTime(now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private void ApplyCurrentTime(string currentTimeText)
    {
        if (CurrentTime != currentTimeText)
        {
            CurrentTime = currentTimeText;
        }
    }

    private StatusSnapshot CaptureStatusSnapshot()
    {
        using var process = Process.GetCurrentProcess();

        var memoryMB = process.PrivateMemorySize64 / (1024f * 1024f);
        var currentMemory = (float)Math.Round(memoryMB, 2);
        float currentCpu = CaptureCpuPercent(process);

        return new StatusSnapshot(
            currentMemory,
            currentCpu);
    }

    private float CaptureCpuPercent(Process process)
    {
        var currentTotalProcessorTime = process.TotalProcessorTime;
        var currentCpuCheckTime = DateTime.UtcNow;
        var cpuUsedMs = (currentTotalProcessorTime - lastTotalProcessorTime).TotalMilliseconds;
        var totalMsPassed = (currentCpuCheckTime - lastCpuCheckTime).TotalMilliseconds;
        float currentCpu = OccupyCPU;

        if (totalMsPassed > 0)
        {
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            currentCpu = (float)Math.Round(cpuUsageTotal * 100, 2);
        }

        lastTotalProcessorTime = currentTotalProcessorTime;
        lastCpuCheckTime = currentCpuCheckTime;
        return currentCpu;
    }

    private void ApplyOccupyMemory(float currentMemory)
    {
        if (Math.Abs(currentMemory - lastMemoryMB) > 0.1f)
        {
            OccupyMemory = currentMemory;
            lastMemoryMB = currentMemory;
        }
    }

    private void ApplyOccupyCPU(float currentCpu)
    {
        if (Math.Abs(currentCpu - OccupyCPU) > 0.1f)
        {
            OccupyCPU = currentCpu;
        }
    }

    private void OnCurrentUserChanged(object? sender, CurrentUser? user)
    {
        RunOnUi(() => UpdateCurrentUserDisplayName(user));
    }

    private void UpdateCurrentUserDisplayName(CurrentUser? user = null)
    {
        user ??= currentUserService.CurrentUser;
        CurrentUserDisplayName = user == null
            ? "操作员 (Operator)"
            : $"{user.DisplayName} ({user.Level})";
    }

    private async void OnSelectedLanguageChanged()
    {
        if (SelectedLanguage == null)
        {
            return;
        }

        if (SelectedLanguage.LanguageType == LanguageType.ZH_CN)
        {
            return;
        }

        LanguageModel unsupportedLanguage = SelectedLanguage;
        await dialogMessageService.ShowInfoAsync("当前阶段仅支持简体中文，其他语言将在后续版本接入。", "语言切换");

        if (SelectedLanguage == unsupportedLanguage)
        {
            SelectedLanguage = Languages.FirstOrDefault(x => x.LanguageType == LanguageType.ZH_CN);
        }
    }

    private void OnMainPlcStateChanged(object? sender, EventArgs e)
    {
        RunOnUi(UpdateMainPlcConnectionState);
    }

    private void UpdateMainPlcConnectionState()
    {
        IPlcDevice? mainPlc;
        try
        {
            if (!deviceRegistry.TryGetDevice<IPlcDevice>(DeviceIds.MainPlc, out mainPlc))
            {
                ClearMainPlcSubscription();
                IsMainPlcConnected = false;
                MainPlcConnectionState = "Disconnected";
                return;
            }
        }
        catch (ObjectDisposedException)
        {
            ClearMainPlcSubscription();
            IsMainPlcConnected = false;
            MainPlcConnectionState = "Disconnected";
            return;
        }

        if (!ReferenceEquals(subscribedMainPlc, mainPlc))
        {
            if (subscribedMainPlc != null)
            {
                subscribedMainPlc.StateChanged -= OnMainPlcStateChanged;
            }

            subscribedMainPlc = mainPlc;
            subscribedMainPlc.StateChanged += OnMainPlcStateChanged;
        }

        IsMainPlcConnected = mainPlc.IsConnected;
        MainPlcConnectionState = mainPlc.State.ToString();
    }

    private void ClearMainPlcSubscription()
    {
        if (subscribedMainPlc == null)
        {
            return;
        }

        subscribedMainPlc.StateChanged -= OnMainPlcStateChanged;
        subscribedMainPlc = null;
    }

    private void RunOnUi(Action action)
    {
        if (messageDispatcher.CheckAccess())
        {
            action();
            return;
        }

        messageDispatcher.Post(action);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            currentUserService.CurrentUserChanged -= OnCurrentUserChanged;
            CompositionTarget.Rendering -= OnRendering;
            ClearMainPlcSubscription();
        }

        base.Dispose(disposing);
    }

    private sealed record StatusSnapshot(float MemoryMB, float CpuPercent);
}

