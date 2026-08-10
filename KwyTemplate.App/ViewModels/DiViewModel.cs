using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.Device;
using KwyTemplate.Flow.Machines;

using KwyTemplate.Contracts.Localization;
namespace KwyTemplate.App.ViewModels;

public sealed class DiViewModel : BindableBase, INavigationAware
{
    private readonly IDeviceRegistry? deviceRegistry;
    private readonly MachineBase? machine;
    private readonly ILocalizationService localizationService;
    private readonly Dictionary<int, bool> lastStates = [];
    private IIoCardDevice? ioCard;
    private CancellationTokenSource? activeCts;
    private Task? refreshTask;
    private bool isReverseChecked;
    private string statusMessage;

    public DiViewModel(IDeviceRegistry? deviceRegistry = null, MachineBase? machine = null, ILocalizationService? localizationService = null)
    {
        this.deviceRegistry = deviceRegistry;
        this.machine = machine;
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        statusMessage = T("Di.Status.Waiting", "等待 IO 输入刷新");
    }

    public ObservableCollection<IoPointModel> InPutPoints { get; } = [];

    public bool IsReverseChecked
    {
        get => isReverseChecked;
        set => SetProperty(ref isReverseChecked, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    private DelegateCommand? clearCountCommand;
    public DelegateCommand ClearCountCommand => clearCountCommand ??= new DelegateCommand(ExecuteClearCountCommand);

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        StartRefresh();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        StopRefresh();
    }


    private string T(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private string TF(string key, string fallback, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, T(key, fallback), args);
    private void StartRefresh()
    {
        if (refreshTask is { IsCompleted: false })
        {
            return;
        }

        LoadNamedInputPoints();
        activeCts = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken);
        refreshTask = RunRefreshLoopAsync(activeCts.Token);
    }

    private void StopRefresh()
    {
        activeCts?.Cancel();
        activeCts?.Dispose();
        activeCts = null;
        refreshTask = null;
        StatusMessage = T("Di.Status.Stopped", "IO 输入刷新已停止");
    }

    private void LoadNamedInputPoints()
    {
        InPutPoints.Clear();
        lastStates.Clear();
        ioCard = TryGetMainIoCard();

        IEnumerable<(int Index, string Name)> points = ioCard?.GetAllInputs()
            .Where(point => !string.IsNullOrWhiteSpace(point.Name))
            .OrderBy(point => point.Index)
            ?? GetEnumDefinitions<Machine_Default_PLC.CardToPc>();

        foreach ((int index, string name) in points)
        {
            bool current = TryReadSnapshot(index);
            InPutPoints.Add(new IoPointModel
            {
                BitIndex = index,
                Name = name,
                IsActive = current,
                TriggerCount = 0,
                LastUpdatedAt = DateTime.Now
            });
            lastStates[index] = current;
        }
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                RefreshInputs();
                await Task.Delay(100, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = TF("Di.Status.RefreshFailed", "IO 输入快照刷新失败：{0}", ex.Message);
                await Task.Delay(500, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    private void RefreshInputs()
    {
        bool hasSnapshot = machine != null;
        StatusMessage = hasSnapshot ? T("Di.Status.Monitoring", "IO 输入快照监控中") : T("Di.Status.NoSnapshot", "未找到机台 IO 快照，当前显示机台定义点位");

        foreach (IoPointModel point in InPutPoints)
        {
            bool current = hasSnapshot ? ReadSnapshot(point.BitIndex) : point.IsActive;
            if (IsReverseChecked)
            {
                current = !current;
            }

            bool previous = lastStates.TryGetValue(point.BitIndex, out bool old) && old;
            if (current && !previous)
            {
                point.TriggerCount++;
            }

            point.IsActive = current;
            point.LastUpdatedAt = DateTime.Now;
            lastStates[point.BitIndex] = current;
        }
    }

    private void ExecuteClearCountCommand()
    {
        foreach (IoPointModel point in InPutPoints)
        {
            point.TriggerCount = 0;
        }
    }

    private bool TryReadSnapshot(int index)
    {
        try
        {
            return ReadSnapshot(index);
        }
        catch
        {
            return false;
        }
    }

    private bool ReadSnapshot(int index)
        => machine != null && machine.TryReadDiSnapshotBit(index, out bool state) && state;

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
            StopRefresh();
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

