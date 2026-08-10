using System.Collections.ObjectModel;
using System.ComponentModel;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Device;
using KwyTemplate.Flow.Machines;

using KwyTemplate.Contracts.Localization;
namespace KwyTemplate.App.ViewModels;

public sealed class DoViewModel : BindableBase, INavigationAware
{
    private readonly IDeviceRegistry? deviceRegistry;
    private readonly MachineBase? machine;
    private readonly IAppNotificationService? notificationService;
    private readonly ILocalizationService localizationService;
    private IIoCardDevice? ioCard;
    private CancellationTokenSource? activeCts;
    private double durationValue = 100;
    private bool isReverseChecked;
    private bool isMillisecondsMode = true;
    private bool isSecondsMode;
    private bool isHoldMode;

    public DoViewModel(
        IDeviceRegistry? deviceRegistry = null,
        MachineBase? machine = null,
        IAppNotificationService? notificationService = null,
        ILocalizationService? localizationService = null)
    {
        this.deviceRegistry = deviceRegistry;
        this.machine = machine;
        this.notificationService = notificationService;
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public ObservableCollection<IoPointModel> OutPutPoints { get; } = [];

    public double DurationValue
    {
        get => durationValue;
        set => SetProperty(ref durationValue, value);
    }

    public bool IsReverseChecked
    {
        get => isReverseChecked;
        set => SetProperty(ref isReverseChecked, value);
    }

    public bool IsMillisecondsMode
    {
        get => isMillisecondsMode;
        set => SetProperty(ref isMillisecondsMode, value);
    }

    public bool IsSecondsMode
    {
        get => isSecondsMode;
        set => SetProperty(ref isSecondsMode, value);
    }

    public bool IsHoldMode
    {
        get => isHoldMode;
        set => SetProperty(ref isHoldMode, value);
    }

    private DelegateCommand<IoPointModel>? ioTriggerCommand;
    public DelegateCommand<IoPointModel> IoTriggerCommand => ioTriggerCommand ??= new DelegateCommand<IoPointModel>(ExecuteIoTriggerCommand);

    private DelegateCommand? clearCountCommand;
    public DelegateCommand ClearCountCommand => clearCountCommand ??= new DelegateCommand(ExecuteClearCountCommand);

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        activeCts?.Dispose();
        activeCts = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken);
        LoadNamedOutputPoints();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        StopActiveOperations();
    }

    private void StopActiveOperations()
    {
        activeCts?.Cancel();
        activeCts?.Dispose();
        activeCts = null;
    }

    private void LoadNamedOutputPoints()
    {
        OutPutPoints.Clear();
        ioCard = TryGetMainIoCard();

        IEnumerable<(int Index, string Name)> points = ioCard?.GetAllOutputs()
            .Where(point => !string.IsNullOrWhiteSpace(point.Name))
            .OrderBy(point => point.Index)
            ?? GetEnumDefinitions<Machine_Default_PLC.PcToCard>();

        foreach ((int index, string name) in points)
        {
            OutPutPoints.Add(new IoPointModel
            {
                BitIndex = index,
                Name = name,
                IsActive = false,
                TriggerCount = 0
            });
        }
    }


    private string T(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }
    private void ExecuteIoTriggerCommand(IoPointModel? item)
    {
        if (item == null)
        {
            return;
        }

        if (machine?.IsRunning == true)
        {
            _ = ShowProductionRunningWarningAsync();
            return;
        }

        ioCard ??= TryGetMainIoCard();
        if (ioCard == null || !ioCard.IsConnected)
        {
            _ = notificationService?.WarningAsync(T("Do.Message.IoCardNotConnected", "IO 卡未连接，无法手动操作输出点。"), T("Do.Title.Output", "IO 输出"));
            return;
        }

        bool activeState = !IsReverseChecked;
        bool inactiveState = IsReverseChecked;

        if (IsHoldMode)
        {
            bool nextState = !item.IsActive;
            ioCard.WriteDoBit(item.BitIndex, nextState ? activeState : inactiveState);
            item.IsActive = nextState;
            item.TriggerCount++;
            item.LastUpdatedAt = DateTime.Now;
            return;
        }

        int durationMs = CalculateDurationMilliseconds();
        if (IsReverseChecked)
        {
            ioCard.WriteDoBit(item.BitIndex, activeState);
            CancellationToken cancellationToken = activeCts?.Token ?? DestroyToken;
            _ = ResetReversePulseAsync(ioCard, item.BitIndex, inactiveState, durationMs, cancellationToken);
        }
        else
        {
            ioCard.WritePulse(item.BitIndex, durationMs);
        }

        item.TriggerCount++;
        item.LastUpdatedAt = DateTime.Now;
    }

    private async Task ShowProductionRunningWarningAsync()
    {
        if (notificationService == null)
        {
            return;
        }

        await notificationService.WarningAsync(T("Do.Message.ProductionRunningCannotOperate", "生产运行中禁止手动操作 IO 输出。"), T("Do.Title.Output", "IO 输出")).ConfigureAwait(true);
    }

    private static async Task ResetReversePulseAsync(
        IIoCardDevice ioCard,
        int channel,
        bool inactiveState,
        int durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(durationMs, cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested && ioCard.IsConnected)
            {
                ioCard.WriteDoBit(channel, inactiveState);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private void ExecuteClearCountCommand()
    {
        foreach (IoPointModel point in OutPutPoints)
        {
            point.TriggerCount = 0;
        }
    }

    private int CalculateDurationMilliseconds()
    {
        double rawValue = Math.Max(1, DurationValue);
        double milliseconds = IsSecondsMode ? rawValue * 1000 : rawValue;
        return (int)Math.Clamp(milliseconds, 1, int.MaxValue);
    }

    private IIoCardDevice? TryGetMainIoCard()
    {
        if (deviceRegistry?.TryGetDevice<IIoCardDevice>(DeviceIds.MainIoCard, out IIoCardDevice? device) == true)
        {
            return device;
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopActiveOperations();
        }

        base.Dispose(disposing);
    }

    private static IEnumerable<(int Index, string Name)> GetEnumDefinitions<TEnum>() where TEnum : struct, Enum
    {
        foreach (Enum value in Enum.GetValues(typeof(TEnum)))
        {
            yield return (Convert.ToInt32(value), GetDescription(value));
        }
    }

    private static string GetDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description ?? value.ToString();
    }
}


