using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions;
using Kwy.MVVM.Core;
using Kwy.UI.WPF.Components;
using KwyTemplate.App.Models;
using KwyTemplate.App.Plc;
using KwyTemplate.Contracts.Security;
using KwyTemplate.Device;
using KwyTemplate.Device.Connections;
using KwyTemplate.Device.Tcp;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using Kwy.UI.WPF.Components.Dialogs;

namespace KwyTemplate.App.ViewModels;

public sealed partial class HomeViewModel : BindableBase
{
    private readonly IDeviceRegistry deviceRegistry;
    private readonly IDeviceConnectionService connectionService;
    private readonly IDialogMessageService dialogMessageService;
    private readonly IPermissionService permissionService;
    private readonly SemaphoreSlim plcWriteLock = new(1, 1);
    private CancellationTokenSource? visionReceiveCts;
    private string currentCountAddress = DemoPlcPoints.Get(DemoPlcPoint.CurrentQuantity).Address;
    private string setQuantityAddress = DemoPlcPoints.Get(DemoPlcPoint.SetQuantity).Address;
    private int visionNgCount;
    private int setQuantity;
    private string visionLastPayload = string.Empty;
    private string statusMessage = "等待设备连接";
    private bool isVisionReceiving;

    public HomeViewModel(
        IDeviceRegistry deviceRegistry,
        IDeviceConnectionService connectionService,
        IDialogMessageService dialogMessageService,
        IPermissionService permissionService)
    {
        this.deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        this.connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        this.dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));
        this.permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));

        CassetteSwitches = new ObservableCollection<PlcBoolPointModel>(
            new[] { CreateMasterCassetteSwitch() }.Concat(DemoPlcPoints.CassetteSwitches().Select(CreateBoolPoint)));

        AlarmMonitorPoints = new ObservableCollection<PlcBoolPointModel>(
            DemoPlcPoints.AlarmMonitors().Select(CreateBoolPoint));

        RegisterMonitorPoints =
        [
            CreateRegisterPoint(DemoPlcPoints.Get(DemoPlcPoint.CurrentQuantity)),
            CreateRegisterPoint(DemoPlcPoints.Get(DemoPlcPoint.SetQuantity))
        ];

    }

    private IPlcDevice MainPlc => deviceRegistry.GetRequiredDevice<IPlcDevice>(DeviceIds.MainPlc);

    private IExternalTcpDevice VisionTcpDevice => deviceRegistry.GetRequiredDevice<IExternalTcpDevice>(DeviceIds.ExternalTcpDevice);

    public ObservableCollection<PlcBoolPointModel> CassetteSwitches { get; }

    public ObservableCollection<PlcBoolPointModel> AlarmMonitorPoints { get; }

    public ObservableCollection<PlcRegisterPointModel> RegisterMonitorPoints { get; }

    #region Command

    private AsyncDelegateCommand? connectDevicesCommand;

    public AsyncDelegateCommand ConnectDevicesCommand =>
        connectDevicesCommand ??= new AsyncDelegateCommand(ExecuteConnectDevicesAsync);

    private async Task ExecuteConnectDevicesAsync()
    {
        await RunDeviceOperationAsync(
            "连接设备",
            async token =>
            {
                await connectionService.ConnectDeviceAsync(DeviceIds.MainPlc, token);
                StatusMessage = "PLC 已连接";
                RaiseConnectionProperties();
            });
    }

    private AsyncDelegateCommand? refreshRegisterCommand;

    public AsyncDelegateCommand RefreshRegisterCommand =>
        refreshRegisterCommand ??= new AsyncDelegateCommand(ExecuteRefreshRegistersAsync);

    private async Task ExecuteRefreshRegistersAsync()
       => await RunDeviceOperationAsync("刷新数据寄存器", RefreshRegisterCoreAsync);

    private async Task RefreshRegisterCoreAsync(CancellationToken cancellationToken)
    {
        foreach (PlcRegisterPointModel point in RegisterMonitorPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(point.Address))
            {
                continue;
            }

            int[] values = await MainPlc.ReadInt32ArrayAsync(point.Address, 1, cancellationToken);
            if (values.Length > 0)
            {
                point.Value = values[0];
                point.LastUpdatedAt = DateTime.Now;
            }
        }

        StatusMessage = $"数据寄存器已刷新：{DateTime.Now:HH:mm:ss}";
    }

    private AsyncDelegateCommand<PlcBoolPointModel>? toggleCassetteSwitchCommand;

    public AsyncDelegateCommand<PlcBoolPointModel> ToggleCassetteSwitchCommand =>
        toggleCassetteSwitchCommand ??= new AsyncDelegateCommand<PlcBoolPointModel>(ExecuteToggleCassetteSwitchAsync);

    private async Task ExecuteToggleCassetteSwitchAsync(PlcBoolPointModel? point)
    {
        if (point == null)
        {
            return;
        }

        bool desiredValue = point.Value;
        if (!permissionService.HasPermission(PermissionCodes.Engineer))
        {
            point.Value = !desiredValue;
            await dialogMessageService.ShowWarningAsync(
                permissionService.GetNoPermissionMessage(PermissionCodes.Engineer),
                "权限不足");
            return;
        }

        try
        {
            if (point.IsMaster)
            {
                await WriteAllCassetteSwitchesAsync(desiredValue, DestroyToken);
            }
            else
            {
                await MainPlc.WriteBoolAsync(point.Address, desiredValue, DestroyToken);
                point.LastUpdatedAt = DateTime.Now;
                UpdateMasterCassetteSwitchState();
                StatusMessage = $"{point.Name} {point.Address}={(desiredValue ? 1 : 0)}";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            point.Value = !desiredValue;
            StatusMessage = $"料盒开关写入失败：{ex.Message}";
            await dialogMessageService.ShowErrorAsync(StatusMessage, "料盒开关");
        }
    }

    private AsyncDelegateCommand? writeVisionNgToPlcCommand;

    public AsyncDelegateCommand WriteVisionNgToPlcCommand =>
        writeVisionNgToPlcCommand ??= new AsyncDelegateCommand(ExecuteWriteVisionNgToPlcAsync);

    private async Task ExecuteWriteVisionNgToPlcAsync()
        => await RunDeviceOperationAsync(
            "写入当前数量",
            async token =>
            {
                await WriteInt32Async(CurrentCountAddress, VisionNgCount, token);
                UpdateRegisterPoint(CurrentCountAddress, VisionNgCount);
                StatusMessage = $"当前数量 {VisionNgCount} 已写入 {CurrentCountAddress}";
            });

    private AsyncDelegateCommand? writeSetQuantityToPlcCommand;

    public AsyncDelegateCommand WriteSetQuantityToPlcCommand =>
        writeSetQuantityToPlcCommand ??= new AsyncDelegateCommand(ExecuteWriteSetQuantityToPlcAsync);

    private async Task ExecuteWriteSetQuantityToPlcAsync()
        => await RunDeviceOperationAsync(
            "写入设定数量",
            async token =>
            {
                await WriteInt32Async(SetQuantityAddress, SetQuantity, token);
                UpdateRegisterPoint(SetQuantityAddress, SetQuantity);
                StatusMessage = $"设定数量 {SetQuantity} 已写入 {SetQuantityAddress}";
            });

    private DelegateCommand? startVisionReceiveCommand;

    public DelegateCommand StartVisionReceiveCommand =>
        startVisionReceiveCommand ??= new DelegateCommand(ExecuteStartVisionReceive, () => !IsVisionReceiving);

    private void ExecuteStartVisionReceive()
    {
        if (IsVisionReceiving)
        {
            return;
        }

        visionReceiveCts = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken);
        IsVisionReceiving = true;
        StatusMessage = "视觉 TCP 接收已启动";
        _ = ReceiveVisionLoopAsync(visionReceiveCts.Token);
    }

    private DelegateCommand? stopVisionReceiveCommand;

    public DelegateCommand StopVisionReceiveCommand =>
        stopVisionReceiveCommand ??= new DelegateCommand(ExecuteStopVisionReceive, () => IsVisionReceiving);

    private void ExecuteStopVisionReceive()
    {
        visionReceiveCts?.Cancel();
        visionReceiveCts?.Dispose();
        visionReceiveCts = null;
        IsVisionReceiving = false;
        StatusMessage = "视觉 TCP 接收已停止";
    }

    #endregion Command

    #region 属性

    public string CurrentCountAddress
    {
        get => currentCountAddress;
        set => SetProperty(ref currentCountAddress, value);
    }

    public string SetQuantityAddress
    {
        get => setQuantityAddress;
        set => SetProperty(ref setQuantityAddress, value);
    }

    public int VisionNgCount
    {
        get => visionNgCount;
        set => SetProperty(ref visionNgCount, value);
    }

    public int SetQuantity
    {
        get => setQuantity;
        set => SetProperty(ref setQuantity, value);
    }

    public string VisionLastPayload
    {
        get => visionLastPayload;
        private set => SetProperty(ref visionLastPayload, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public bool IsPlcConnected =>
        TryGetMainPlc(out var plc) && plc.IsConnected;

    public bool IsVisionConnected =>
        TryGetVisionTcpDevice(out var device) && device.IsConnected;

    public bool IsVisionReceiving
    {
        get => isVisionReceiving;
        private set
        {
            if (SetProperty(ref isVisionReceiving, value))
            {
                startVisionReceiveCommand?.RaiseCanExecuteChanged();
                stopVisionReceiveCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    #endregion 属性

    private async Task WriteAllCassetteSwitchesAsync(bool value, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;
        foreach (PlcBoolPointModel point in CassetteSwitches.Where(item => !item.IsMaster))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MainPlc.WriteBoolAsync(point.Address, value, cancellationToken);
            point.Value = value;
            point.LastUpdatedAt = now;
        }

        PlcBoolPointModel master = CassetteSwitches.First(item => item.IsMaster);
        master.Value = value;
        master.LastUpdatedAt = now;
        StatusMessage = $"全部料盒锁已{(value ? "打开" : "关闭")}";
    }

    private void UpdateMasterCassetteSwitchState()
    {
        PlcBoolPointModel? master = CassetteSwitches.FirstOrDefault(item => item.IsMaster);
        if (master == null)
        {
            return;
        }

        master.Value = CassetteSwitches.Where(item => !item.IsMaster).All(item => item.Value);
        master.LastUpdatedAt = DateTime.Now;
    }

    private async Task ReceiveVisionLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int length = await VisionTcpDevice.ReadAsync(buffer, cancellationToken);
                if (length <= 0)
                {
                    continue;
                }

                string payload = Encoding.UTF8.GetString(buffer, 0, length).Trim();
                VisionLastPayload = payload;

                if (TryParseFirstInt32(payload, out int ngCount))
                {
                    VisionNgCount = ngCount;
                    await WriteInt32Async(CurrentCountAddress, ngCount, cancellationToken);
                    UpdateRegisterPoint(CurrentCountAddress, ngCount);
                    StatusMessage = $"视觉 NG={ngCount} 已写入 {CurrentCountAddress}";
                }
                else
                {
                    StatusMessage = $"视觉数据无法解析为 Int32：{payload}";
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"视觉 TCP 接收失败：{ex.Message}";
            await dialogMessageService.ShowErrorAsync(StatusMessage, "视觉通信");
        }
        finally
        {
            IsVisionReceiving = false;
        }
    }

    private bool TryGetMainPlc(out IPlcDevice mainPlc)
    {
        try
        {
            return deviceRegistry.TryGetDevice(DeviceIds.MainPlc, out mainPlc);
        }
        catch (ObjectDisposedException)
        {
            mainPlc = null!;
            return false;
        }
    }

    private bool TryGetVisionTcpDevice(out IExternalTcpDevice device)
    {
        try
        {
            return deviceRegistry.TryGetDevice(DeviceIds.ExternalTcpDevice, out device);
        }
        catch (ObjectDisposedException)
        {
            device = null!;
            return false;
        }
    }

    private async Task WriteInt32Async(string address, int value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("PLC 地址不能为空。");
        }

        await plcWriteLock.WaitAsync(cancellationToken);
        try
        {
            await MainPlc.WriteInt32Async(address, value, cancellationToken);
        }
        finally
        {
            plcWriteLock.Release();
        }
    }

    private async Task RunDeviceOperationAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken, cancellationToken);
            await operation(linked.Token);
        }
        catch (OperationCanceledException) when (DestroyToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"{operationName}失败：{ex.Message}";
            await dialogMessageService.ShowErrorAsync(StatusMessage, operationName);
        }
    }

    private void UpdateRegisterPoint(string address, int value)
    {
        PlcRegisterPointModel? point = RegisterMonitorPoints.FirstOrDefault(
            item => string.Equals(item.Address, address, StringComparison.OrdinalIgnoreCase));
        if (point == null)
        {
            return;
        }

        point.Value = value;
        point.LastUpdatedAt = DateTime.Now;
    }

    private void RaiseConnectionProperties()
    {
        RaisePropertyChanged(nameof(IsPlcConnected));
        RaisePropertyChanged(nameof(IsVisionConnected));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ExecuteStopVisionReceive();
            plcWriteLock.Dispose();
        }

        base.Dispose(disposing);
    }

    private static PlcBoolPointModel CreateBoolPoint(DemoPlcPointDefinition definition)
        => new()
        {
            Address = definition.Address,
            Name = definition.DisplayName
        };

    private static PlcBoolPointModel CreateMasterCassetteSwitch()
        => new()
        {
            Address = "ALL",
            Name = "全部料盒锁",
            IsMaster = true
        };

    private static PlcRegisterPointModel CreateRegisterPoint(DemoPlcPointDefinition definition)
        => new()
        {
            Address = definition.Address,
            Name = definition.DisplayName
        };

    private static bool TryParseFirstInt32(string text, out int value)
    {
        Match match = Int32Regex().Match(text);
        return int.TryParse(match.Value, out value);
    }

    [GeneratedRegex(@"-?\d+")]
    private static partial Regex Int32Regex();
}
