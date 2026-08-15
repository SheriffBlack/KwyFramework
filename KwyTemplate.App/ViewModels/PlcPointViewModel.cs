using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using Kwy.MVVM.Core;
using Kwy.UI.DataGrids;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Components.Logging;
using Kwy.UI.WPF.Controls.Helpers;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using System.Windows.Controls;
using Kwy.UI.Enums;

namespace KwyTemplate.App.ViewModels;

public sealed class PlcPointViewModel : BindableBase
{
    private readonly IDeviceRegistry? deviceRegistry;
    private readonly IInputDialogService? inputDialogService;
    private readonly IAppNotificationService? notificationService;
    private readonly KwyLogService? logService;
    private readonly MachineBase machine;
    private readonly ILocalizationService? localizationService;
    private readonly DispatcherTimer refreshTimer;
    private readonly ObservableCollection<IDataGridColumnDescriptor> columns = [];
    private IPlcDevice? plc;
    private bool isRefreshing;
    private string statusMessage;

    public PlcPointViewModel(
        MachineBase machine,
        IDeviceRegistry? deviceRegistry = null,
        IInputDialogService? inputDialogService = null,
        IAppNotificationService? notificationService = null,
        KwyLogService? logService = null,
        ILocalizationService? localizationService = null)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.deviceRegistry = deviceRegistry;
        this.inputDialogService = inputDialogService;
        this.notificationService = notificationService;
        this.logService = logService;
        this.localizationService = localizationService;
        statusMessage = localizationService?.T("PlcPoint.Status.Waiting", "等待 PLC 点位刷新") ?? "等待 PLC 点位刷新";
        SyncColumns();
        LoadPoints();
        if (localizationService != null)
        {
            localizationService.LanguageChanged += OnLanguageChanged;
        }

        refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        refreshTimer.Tick += OnRefreshTimerTick;
        refreshTimer.Start();
    }

    public ObservableCollection<PlcPointModel> Points { get; } = [];

    public ObservableCollection<IDataGridColumnDescriptor> Columns => columns;

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    private AsyncDelegateCommand<PlcPointModel>? writeOtherCommand;
    public AsyncDelegateCommand<PlcPointModel> WriteOtherCommand => writeOtherCommand ??= new AsyncDelegateCommand<PlcPointModel>(ExecuteWriteOtherAsync);

    private AsyncDelegateCommand<PlcPointModel>? writeZeroCommand;
    public AsyncDelegateCommand<PlcPointModel> WriteZeroCommand => writeZeroCommand ??= new AsyncDelegateCommand<PlcPointModel>(point => ExecuteWriteShortcutAsync(point, "0"));

    private AsyncDelegateCommand<PlcPointModel>? writeOneCommand;
    public AsyncDelegateCommand<PlcPointModel> WriteOneCommand => writeOneCommand ??= new AsyncDelegateCommand<PlcPointModel>(point => ExecuteWriteShortcutAsync(point, "1"));

    private void SyncColumns()
    {
        columns.Clear();
        columns.Add(CreateTextColumn(nameof(PlcPointModel.DisplayName), localizationService?.T("PlcPoint.Column.Description", "描述") ?? "描述", 300));
        columns.Add(CreateTextColumn(nameof(PlcPointModel.Address), localizationService?.T("PlcPoint.Column.Address", "地址") ?? "地址", 100));
        columns.Add(CreateTextColumn(nameof(PlcPointModel.DataTypeName), localizationService?.T("PlcPoint.Column.Type", "类型") ?? "类型", 80));
        columns.Add(new WpfDataGridColumnOptions
        {
            ParameterId = nameof(PlcPointModel.ValueText),
            DisplayName = localizationService?.T("PlcPoint.Column.RealTimeValue", "实时值") ?? "实时值",
            BindingPath = nameof(PlcPointModel.ValueText),
            Width = new DataGridLength(120),
            ElementStyleKey = "PlcPointValueTextBlockStyle",
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        });
        columns.Add(new WpfDataGridColumnOptions
        {
            // 模板列不参与测值判定着色；留空可保持与原静态 DataGridTemplateColumn 相同的单元格布局。
            ParameterId = string.Empty,
            DisplayName = localizationService?.T("PlcPoint.Column.WriteControl", "控制写入") ?? "控制写入",
            ColumnType = DataGridColumnType.Template,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplateKey = "PlcPointWriteControlTemplate",
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        });
    }

    private static IDataGridColumnDescriptor CreateTextColumn(string bindingPath, string displayName, double width)
        => new WpfDataGridColumnOptions
        {
            ParameterId = bindingPath,
            DisplayName = displayName,
            BindingPath = bindingPath,
            Width = new DataGridLength(width),
            ElementStyleKey = "CenterAlignedTextBlock",
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        };

    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        SyncColumns();
        LoadPoints();
    }

    private void LoadPoints()
    {
        Points.Clear();
        foreach (MachinePlcPointDefinition point in machine.PlcPointDefinitions.OrderBy(static item => item.Address, StringComparer.OrdinalIgnoreCase))
        {
            Points.Add(new PlcPointModel(point));
        }

        StatusMessage = Points.Count == 0
            ? localizationService?.T("PlcPoint.Status.NoPoints", "当前机型未注册 PLC 点位。") ?? "当前机型未注册 PLC 点位。"
            : localizationService?.TF("PlcPoint.Status.PointsLoaded", "已加载 {0} 个 PLC 点位。", Points.Count) ?? string.Format(CultureInfo.CurrentCulture, "已加载 {0} 个 PLC 点位。", Points.Count);
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (isRefreshing)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            await ExecuteRefreshAsync();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async Task ExecuteRefreshAsync()
    {
        plc = TryGetMainPlc();
        if (plc == null || !plc.IsConnected)
        {
            StatusMessage = localizationService?.T("PlcPoint.Status.PlcDisconnected", "PLC 未连接。") ?? "PLC 未连接。";
            foreach (PlcPointModel point in Points)
            {
                point.ValueText = localizationService?.T("PlcPoint.Value.Disconnected", "未连接") ?? "未连接";
                point.StatusMessage = localizationService?.T("PlcPoint.Status.PlcDisconnectedShort", "PLC 未连接") ?? "PLC 未连接";
            }

            return;
        }

        foreach (PlcPointModel point in Points)
        {
            await ReadPointAsync(point, DestroyToken);
        }

        StatusMessage = localizationService?.TF("PlcPoint.Status.Refreshing", "PLC 点位刷新中：{0}", DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)) ?? string.Format(CultureInfo.CurrentCulture, "PLC 点位刷新中：{0}", DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }

    private async Task ExecuteWriteAsync(PlcPointModel? point)
    {
        if (point == null || point.IsReadOnly)
        {
            return;
        }

        if (!await EnsurePlcReadyForWriteAsync(point).ConfigureAwait(false))
        {
            return;
        }

        string writeValue = point.WriteValueText;
        string pointName = FormatPointName(point);
        string plcName = GetPlcDisplayName();

        try
        {
            PlcWriteOperation writeOperation = CreateWriteOperation(point);
            writeValue = writeOperation.ActualValue;
            await writeOperation.ExecuteAsync(DestroyToken).ConfigureAwait(false);
            point.StatusMessage = localizationService?.T("PlcPoint.Status.WriteSuccess", "写入成功") ?? "写入成功";
            StatusMessage = localizationService?.TF("PlcPoint.Status.Written", "已写入：{0}", point.DisplayName) ?? string.Format(CultureInfo.CurrentCulture, "已写入：{0}", point.DisplayName);
            AddManualPlcWriteLog(point, writeValue, plcName);

            if (notificationService != null)
            {
                await notificationService.SuccessAsync(localizationService?.TF("PlcPoint.Message.WriteSuccess", "{{{0}}}{{{1}}}写入{{{2}}}成功！", plcName, pointName, writeValue) ?? string.Format(CultureInfo.CurrentCulture, "{{{0}}}{{{1}}}写入{{{2}}}成功！", plcName, pointName, writeValue), localizationService?.T("PlcPoint.Title.Write", "PLC 写入") ?? "PLC 写入", writeLog: false).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string failureMessage = FormatWriteFailureMessage(ex);
            point.StatusMessage = failureMessage;
            StatusMessage = localizationService?.TF("PlcPoint.Status.WriteFailed", "写入失败：{0}", failureMessage) ?? string.Format(CultureInfo.CurrentCulture, "写入失败：{0}", failureMessage);

            if (notificationService != null)
            {
                await notificationService.ErrorAsync(localizationService?.TF("PlcPoint.Message.WriteFailed", "{{{0}}}{{{1}}}写入{{{2}}}失败！\n{3}", plcName, pointName, writeValue, failureMessage) ?? string.Format(CultureInfo.CurrentCulture, "{{{0}}}{{{1}}}写入{{{2}}}失败！\n{3}", plcName, pointName, writeValue, failureMessage), localizationService?.T("PlcPoint.Title.WriteFailed", "PLC 写入失败") ?? "PLC 写入失败", writeLog: false).ConfigureAwait(false);
            }

            return;
        }

        try
        {
            await ReadPointAsync(point, DestroyToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            point.StatusMessage = localizationService?.TF("PlcPoint.Status.WriteSuccessReadFailed", "写入成功，刷新读取失败：{0}", FormatWriteFailureMessage(ex)) ?? string.Format(CultureInfo.CurrentCulture, "写入成功，刷新读取失败：{0}", FormatWriteFailureMessage(ex));
        }
    }

    private async Task ExecuteWriteShortcutAsync(PlcPointModel? point, string value)
    {
        if (point == null || point.IsReadOnly)
        {
            return;
        }

        if (!await EnsurePlcReadyForWriteAsync(point).ConfigureAwait(false))
        {
            return;
        }

        point.WriteValueText = value;
        await ExecuteWriteAsync(point).ConfigureAwait(false);
    }

    private async Task ExecuteWriteOtherAsync(PlcPointModel? point)
    {
        if (point == null || point.IsReadOnly)
        {
            return;
        }

        if (!await EnsurePlcReadyForWriteAsync(point).ConfigureAwait(false))
        {
            return;
        }

        if (inputDialogService == null)
        {
            StatusMessage = localizationService?.T("PlcPoint.Status.InputDialogMissing", "输入弹窗服务未注册，无法写入其他值。") ?? "输入弹窗服务未注册，无法写入其他值。";
            point.StatusMessage = localizationService?.T("PlcPoint.Status.InputDialogMissingShort", "输入弹窗服务未注册") ?? "输入弹窗服务未注册";
            return;
        }

        InputDialogResult result = await inputDialogService.ShowAsync(CreateWriteDialogOptions(point)).ConfigureAwait(false);
        if (!result.IsConfirmed)
        {
            return;
        }

        point.WriteValueText = NormalizeWriteValue(point.Definition.DataType, result.Value);
        await ExecuteWriteAsync(point).ConfigureAwait(false);
    }

    private async Task ReadPointAsync(PlcPointModel point, CancellationToken cancellationToken)
    {
        if (plc == null)
        {
            return;
        }

        try
        {
            Type dataType = point.Definition.DataType;
            string value = await ReadValueTextAsync(point.Address, dataType, cancellationToken);
            point.ValueText = value;
            point.LastUpdatedAt = DateTime.Now;
            point.StatusMessage = "OK";
        }
        catch (Exception ex)
        {
            point.StatusMessage = ex.Message;
        }
    }

    private async Task<bool> EnsurePlcReadyForWriteAsync(PlcPointModel point)
    {
        plc = TryGetMainPlc();
        if (plc == null)
        {
            StatusMessage = localizationService?.T("PlcPoint.Status.MainPlcMissing", "未找到主 PLC 设备。") ?? "未找到主 PLC 设备。";
            point.StatusMessage = localizationService?.T("PlcPoint.Status.PlcNotRegistered", "PLC 未注册") ?? "PLC 未注册";
            if (notificationService != null)
            {
                await notificationService.ErrorAsync(localizationService?.T("PlcPoint.Message.MainPlcMissing", "未找到主 PLC 设备，请检查设备注册。") ?? "未找到主 PLC 设备，请检查设备注册。", localizationService?.T("PlcPoint.Status.PlcNotRegistered", "PLC 未注册") ?? "PLC 未注册", writeLog: false).ConfigureAwait(false);
            }

            return false;
        }

        if (plc.IsConnected)
        {
            return true;
        }

        StatusMessage = localizationService?.T("PlcPoint.Status.PlcDisconnectedCannotWrite", "PLC 未连接，无法写入点位。") ?? "PLC 未连接，无法写入点位。";
        point.StatusMessage = localizationService?.T("PlcPoint.Status.PlcDisconnectedShort", "PLC 未连接") ?? "PLC 未连接";
        if (notificationService != null)
        {
            await notificationService.WarningAsync(localizationService?.T("PlcPoint.Message.PlcDisconnectedCannotWrite", "PLC 未连接，请先连接 PLC 后再写入点位。") ?? "PLC 未连接，请先连接 PLC 后再写入点位。", localizationService?.T("PlcPoint.Status.PlcDisconnectedShort", "PLC 未连接") ?? "PLC 未连接", writeLog: false).ConfigureAwait(false);
        }

        return false;
    }

    private async Task<string> ReadValueTextAsync(string address, Type dataType, CancellationToken cancellationToken)
    {
        if (plc == null)
        {
            return string.Empty;
        }

        if (dataType == typeof(bool))
        {
            return (await plc.ReadBoolAsync(address, cancellationToken)).ToString();
        }

        if (dataType == typeof(short))
        {
            return (await plc.ReadInt16Async(address, cancellationToken)).ToString(CultureInfo.InvariantCulture);
        }

        if (dataType == typeof(ushort))
        {
            short value = await plc.ReadInt16Async(address, cancellationToken);
            return unchecked((ushort)value).ToString(CultureInfo.InvariantCulture);
        }

        if (dataType == typeof(int))
        {
            int[] values = await plc.ReadInt32ArrayAsync(address, 1, cancellationToken);
            return values.Length > 0 ? values[0].ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        if (dataType == typeof(uint))
        {
            int[] values = await plc.ReadInt32ArrayAsync(address, 1, cancellationToken);
            return values.Length > 0 ? unchecked((uint)values[0]).ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        if (dataType == typeof(float))
        {
            return (await plc.ReadFloatAsync(address, cancellationToken)).ToString(CultureInfo.InvariantCulture);
        }

        throw new NotSupportedException(localizationService?.TF("PlcPoint.Message.UnsupportedReadType", "不支持读取的数据类型：{0}", dataType.Name) ?? string.Format(CultureInfo.CurrentCulture, "不支持读取的数据类型：{0}", dataType.Name));
    }

    private PlcWriteOperation CreateWriteOperation(PlcPointModel point)
    {
        if (plc == null)
        {
            throw new InvalidOperationException(localizationService?.T("PlcPoint.Status.PlcDisconnected", "PLC 未连接。") ?? "PLC 未连接。");
        }

        Type dataType = point.Definition.DataType;
        string raw = point.WriteValueText;

        if (dataType == typeof(bool))
        {
            bool value = ParseBoolean(raw);
            return new PlcWriteOperation(value.ToString(), token => plc.WriteBoolAsync(point.Address, value, token));
        }

        if (dataType == typeof(short))
        {
            short value = short.Parse(raw, CultureInfo.InvariantCulture);
            return new PlcWriteOperation(value.ToString(CultureInfo.InvariantCulture), token => plc.WriteInt16Async(point.Address, value, token));
        }

        if (dataType == typeof(ushort))
        {
            ushort value = ushort.Parse(raw, CultureInfo.InvariantCulture);
            short actualValue = unchecked((short)value);
            return new PlcWriteOperation(actualValue.ToString(CultureInfo.InvariantCulture), token => plc.WriteInt16Async(point.Address, actualValue, token));
        }

        if (dataType == typeof(int))
        {
            int value = int.Parse(raw, CultureInfo.InvariantCulture);
            return new PlcWriteOperation(value.ToString(CultureInfo.InvariantCulture), token => plc.WriteInt32Async(point.Address, value, token));
        }

        if (dataType == typeof(uint))
        {
            uint value = uint.Parse(raw, CultureInfo.InvariantCulture);
            int actualValue = unchecked((int)value);
            return new PlcWriteOperation(actualValue.ToString(CultureInfo.InvariantCulture), token => plc.WriteInt32Async(point.Address, actualValue, token));
        }

        if (dataType == typeof(float))
        {
            float value = float.Parse(raw, CultureInfo.InvariantCulture);
            return new PlcWriteOperation(value.ToString(CultureInfo.InvariantCulture), token => plc.WriteFloatAsync(point.Address, value, token));
        }

        throw new NotSupportedException(localizationService?.TF("PlcPoint.Message.UnsupportedWriteType", "不支持写入的数据类型：{0}", dataType.Name) ?? string.Format(CultureInfo.CurrentCulture, "不支持写入的数据类型：{0}", dataType.Name));
    }

    private sealed record PlcWriteOperation(string ActualValue, Func<CancellationToken, Task> ExecuteAsync);

    private void AddManualPlcWriteLog(PlcPointModel point, string writeValue, string plcName)
    {
        string pointName = FormatPointName(point);
        logService?.Info(localizationService?.TF("PlcPoint.Log.ManualWriteSuccess", "{0}: 手动写入 {1} = {2} 成功。", plcName, pointName, writeValue) ?? string.Format(CultureInfo.CurrentCulture, "{0}: 手动写入 {1} = {2} 成功。", plcName, pointName, writeValue));
    }

    private string GetPlcDisplayName()
    {
        string? deviceName = plc?.DeviceName;
        return string.IsNullOrWhiteSpace(deviceName) ? localizationService?.T("Device.MainPlc", "主 PLC") ?? "主 PLC" : deviceName;
    }

    private static string FormatPointName(PlcPointModel point)
        => string.IsNullOrWhiteSpace(point.Address)
            ? point.DisplayName
            : $"{point.DisplayName}({point.Address})";

    private string FormatWriteFailureMessage(Exception exception)
    {
        string message = exception.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return localizationService?.T("Common.UnknownError", "未知错误。") ?? "未知错误。";
        }

        const string hslFailedMarker = " failed: ";
        int markerIndex = message.IndexOf(hslFailedMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            message = message[(markerIndex + hslFailedMarker.Length)..];
        }

        message = message.Replace("PLC is not connected.", localizationService?.T("PlcPoint.Status.PlcDisconnected", "PLC 未连接。") ?? "PLC 未连接。", StringComparison.OrdinalIgnoreCase);
        message = message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(message) ? localizationService?.T("Common.UnknownError", "未知错误。") ?? "未知错误。" : message;
    }

    private IPlcDevice? TryGetMainPlc()
    {
        if (deviceRegistry?.TryGetDevice<IPlcDevice>(DeviceIds.MainPlc, out IPlcDevice? device) == true)
        {
            return device;
        }

        return null;
    }

    private bool ParseBoolean(string value)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            return number != 0;
        }

        throw new FormatException(localizationService?.T("PlcPoint.Message.BoolValueHint", "Bool 值只支持 True、False、1、0。") ?? "Bool 值只支持 True、False、1、0。");
    }

    private InputDialogOptions CreateWriteDialogOptions(PlcPointModel point)
    {
        Type dataType = point.Definition.DataType;
        (decimal? minimum, decimal? maximum) = GetNumericRange(dataType);

        return new InputDialogOptions
        {
            Title = localizationService?.T("PlcPoint.Dialog.WriteTitle", "写入 PLC 点位") ?? "写入 PLC 点位",
            Message = localizationService?.TF("PlcPoint.Dialog.WriteMessage", "请输入 {0}（{1}）的写入值。", point.DisplayName, point.Address) ?? string.Format(CultureInfo.CurrentCulture, "请输入 {0}（{1}）的写入值。", point.DisplayName, point.Address),
            Label = localizationService?.T("PlcPoint.Dialog.WriteValue", "写入值") ?? "写入值",
            DefaultValue = GetDialogDefaultValue(point),
            InputType = dataType == typeof(bool) ? InputDialogType.Text : InputDialogType.Number,
            Minimum = minimum,
            Maximum = maximum,
            Unit = GetDialogUnit(dataType),
            ConfirmButtonText = localizationService?.T("Common.Confirm", "确定") ?? "确定",
            CancelButtonText = localizationService?.T("Common.Cancel", "取消") ?? "取消",
            ShowCancelButton = true
        };
    }

    private static string GetDialogDefaultValue(PlcPointModel point)
        => string.IsNullOrWhiteSpace(point.WriteValueText) ? point.ValueText : point.WriteValueText;

    private string NormalizeWriteValue(Type dataType, string value)
    {
        if (dataType == typeof(bool))
        {
            return ParseBoolean(value).ToString();
        }

        return value;
    }

    private static (decimal? Minimum, decimal? Maximum) GetNumericRange(Type dataType)
    {
        if (dataType == typeof(short))
        {
            return (short.MinValue, short.MaxValue);
        }

        if (dataType == typeof(ushort))
        {
            return (ushort.MinValue, ushort.MaxValue);
        }

        if (dataType == typeof(int))
        {
            return (int.MinValue, int.MaxValue);
        }

        if (dataType == typeof(uint))
        {
            return (uint.MinValue, uint.MaxValue);
        }

        return (null, null);
    }

    private static string GetDialogUnit(Type dataType)
        => dataType == typeof(bool) ? "True/False/1/0" : dataType.Name;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Stop();
            refreshTimer.Tick -= OnRefreshTimerTick;
            if (localizationService != null)
            {
                localizationService.LanguageChanged -= OnLanguageChanged;
            }
        }

        base.Dispose(disposing);
    }
}
