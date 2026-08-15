using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.App.ViewModels;

public sealed class StandardViewModel : BindableBase
{
    private readonly IMesStandardSampleService mesStandardSampleService;
    private readonly IProductionContext productionContext;
    private readonly MachineBase machine;
    private readonly MesConnectionStatus mesConnectionStatus;
    private readonly IAppNotificationService notificationService;
    private readonly StandardSampleState sampleState;
    private readonly ILocalizationService localizationService;
    private readonly IDisposable stationLimitsAppliedSubscription;
    private AsyncDelegateCommand? queryStandardCommand;
    private AsyncDelegateCommand? queryConfirmCommand;
    private bool disposed;

    public StandardViewModel(
        IMesStandardSampleService mesStandardSampleService,
        IProductionContext productionContext,
        MachineBase machine,
        MesConnectionStatus mesConnectionStatus,
        IAppNotificationService notificationService,
        StandardSampleState sampleState,
        ILocalizationService localizationService,
        IMessageBus messageBus)
    {
        this.mesStandardSampleService = mesStandardSampleService ?? throw new ArgumentNullException(nameof(mesStandardSampleService));
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(messageBus);
        this.localizationService.LanguageChanged += OnLanguageChanged;
        stationLimitsAppliedSubscription = messageBus.Subscribe<StandardViewModel, StationLimitsAppliedMessage>(
            this,
            static (viewModel, _) => viewModel.RefreshDefaultLimitUnits());

        EnsureLimitItems(StandardSample.LimitItems);
        EnsureLimitItems(ConfirmSample.LimitItems);
        StandardSample.LimitItems.CollectionChanged += OnLimitItemsChanged;
        ConfirmSample.LimitItems.CollectionChanged += OnLimitItemsChanged;
        this.mesConnectionStatus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MesConnectionStatus.State))
            {
                RaisePropertyChanged(nameof(AreLimitTextBoxesReadOnly));
            }
        };
    }

    public StandardSamplePanelModel StandardSample => sampleState.StandardSample;

    public StandardSamplePanelModel ConfirmSample => sampleState.ConfirmSample;

    public bool AreLimitTextBoxesReadOnly => mesConnectionStatus.State == MesConnectionState.Online;

    public AsyncDelegateCommand QueryStandardCommand => queryStandardCommand ??= new AsyncDelegateCommand(() => QuerySampleAsync(StandardSample));

    public AsyncDelegateCommand QueryConfirmCommand => queryConfirmCommand ??= new AsyncDelegateCommand(() => QuerySampleAsync(ConfirmSample));

    private async Task QuerySampleAsync(StandardSamplePanelModel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (string.IsNullOrWhiteSpace(productionContext.WorkOrderNo))
        {
            await notificationService.WarningAsync(localizationService.T("Standard.Message.WorkOrderRequired", "请先输入或扫描工单。"), localizationService.T("Standard.Title.WorkOrderEmpty", "工单为空")).ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(productionContext.EquipmentNo))
        {
            await notificationService.WarningAsync(localizationService.T("Standard.Message.EquipmentRequired", "请先输入或扫描机台号。"), localizationService.T("Standard.Title.EquipmentEmpty", "机台号为空")).ConfigureAwait(true);
            return;
        }

        string sampleCode = panel.SampleCode.Trim();
        if (string.IsNullOrWhiteSpace(sampleCode))
        {
            await notificationService.WarningAsync(localizationService.TF("Standard.Message.SampleCodeRequired", "请输入{0}编号。", GetPanelTitle(panel)), localizationService.TF("Standard.Title.SampleCodeEmpty", "{0}编号为空", GetPanelTitle(panel))).ConfigureAwait(true);
            return;
        }

        panel.IsQuerying = true;
        panel.ClearResult();

        try
        {
            var request = new MesStandardSampleRequest(CreateMesContext(), productionContext.WorkOrderNo, sampleCode);
            MesResult<MesStandardSampleSetup> result = await mesStandardSampleService.GetStandardSampleAsync(request, DestroyToken).ConfigureAwait(true);

            if (!IsMesAccepted(result) || result.Data == null)
            {
                panel.StatusMessage = result.Message;
                await notificationService.ErrorAsync(MesFailureMessageFormatter.Format(localizationService.TF("Standard.Action.QueryWithTitle", "{0}查询", GetPanelTitle(panel)), result), GetPanelTitle(panel)).ConfigureAwait(true);
                return;
            }

            ApplySampleSetup(panel, result.Data);
            await ResetStandardSampleExpiredSignalIfNeededAsync(panel).ConfigureAwait(true);
            await WarnIfSampleExpiresSoonAsync(panel).ConfigureAwait(true);
            panel.StatusMessage = localizationService.T("Standard.Message.QuerySuccess", "查询成功");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            panel.StatusMessage = ex.Message;
            await notificationService.ErrorAsync(localizationService.TF("Standard.Message.QueryException", "{0}查询异常：\n{1}", GetPanelTitle(panel), ex.Message), GetPanelTitle(panel), ex).ConfigureAwait(true);
        }
        finally
        {
            panel.IsQuerying = false;
        }
    }


    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        RefreshLimitItemLocalization(StandardSample.LimitItems);
        RefreshLimitItemLocalization(ConfirmSample.LimitItems);
    }

    private static void RefreshLimitItemLocalization(IEnumerable<StandardSampleLimitItemModel> items)
    {
        foreach (StandardSampleLimitItemModel item in items)
        {
            item.RefreshLocalization();
        }
    }
    private string GetPanelTitle(StandardSamplePanelModel panel)
        => ReferenceEquals(panel, StandardSample)
            ? localizationService.T("Standard.Header.StandardSample", "标准件")
            : localizationService.T("Standard.Header.ConfirmSample", "确认件");
    private MesRequestContext CreateMesContext()
        => new(
            MachineId: productionContext.EquipmentNo,
            MachineName: machine.MachineName,
            OperatorId: productionContext.OperatorNo,
            WorkOrderNo: productionContext.WorkOrderNo);

    private void ApplySampleSetup(StandardSamplePanelModel panel, MesStandardSampleSetup setup)
    {
        panel.IssueDate = GetParameterValue(setup.Parameters, "IssueDate") ?? GetParameterValue(setup.Parameters, "StartDate") ?? string.Empty;
        panel.ExpireDate = GetParameterValue(setup.Parameters, "ExpireDate") ?? GetParameterValue(setup.Parameters, "EndDate") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(setup.SampleCode))
        {
            panel.SampleCode = setup.SampleCode;
        }

        ApplyMeasurementLimits(panel, setup.MeasurementLimits);
    }

    private void ApplyMeasurementLimits(StandardSamplePanelModel panel, IReadOnlyList<MesMeasurementLimit> limits)
    {
        panel.LimitItems.Clear();

        if (limits.Count == 0)
        {
            EnsureLimitItems(panel.LimitItems);
            return;
        }

        foreach (MesMeasurementLimit limit in limits)
        {
            string code = NormalizeLimitCode(limit.ParameterId) ?? NormalizeLimitCode(limit.DisplayName) ?? limit.ParameterId;
            string displayName = string.IsNullOrWhiteSpace(limit.DisplayName) ? code : limit.DisplayName;
            var item = new StandardSampleLimitItemModel(code, displayName, localizationService)
            {
                LowerLimit = FormatNumber(limit.LowerLimit),
                UpperLimit = FormatNumber(limit.UpperLimit),
                StandardValue = FormatNumber(limit.StandardValue),
                Unit = limit.Unit ?? string.Empty,
                SerialNo = limit.SerialNo ?? string.Empty,
                MeterType = limit.MeterType ?? string.Empty,
                ItemName = limit.ItemName ?? displayName,
                Frequency = limit.Frequency ?? string.Empty,
                FrequencyUnit = limit.FrequencyUnit ?? string.Empty
            };

            panel.LimitItems.Add(item);
        }
    }

    private void EnsureLimitItems(ObservableCollection<StandardSampleLimitItemModel> limitItems)
    {
        if (limitItems.Count > 0)
        {
            return;
        }

        foreach (string testName in GetMachineCheckTestNames())
        {
            string code = NormalizeLimitCode(testName) ?? testName;
            if (!limitItems.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                limitItems.Add(new StandardSampleLimitItemModel(code, code, localizationService)
                {
                    Unit = ResolveDefaultLimitUnit(code) ?? string.Empty
                });
            }
        }
    }

    private void RefreshDefaultLimitUnits()
    {
        ApplyDefaultLimitUnits(StandardSample.LimitItems);
        ApplyDefaultLimitUnits(ConfirmSample.LimitItems);
    }

    private void ApplyDefaultLimitUnits(IEnumerable<StandardSampleLimitItemModel> items)
    {
        foreach (StandardSampleLimitItemModel item in items)
        {
            bool hasManualOrMesValues = !string.IsNullOrWhiteSpace(item.LowerLimit)
                || !string.IsNullOrWhiteSpace(item.UpperLimit)
                || !string.IsNullOrWhiteSpace(item.StandardValue);
            if (hasManualOrMesValues && !string.IsNullOrWhiteSpace(item.Unit))
            {
                continue;
            }

            string? unit = ResolveDefaultLimitUnit(item.Code);
            if (unit != null)
            {
                item.Unit = unit;
            }
        }
    }

    private string? ResolveDefaultLimitUnit(string? code)
    {
        string? normalizedCode = NormalizeLimitCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        foreach (TestStationModel station in machine.TestStations.Where(static station => station.ShowInResultGrid))
        {
            foreach (KeyValuePair<string, StationMeasurementLimit> pair in station.TestLimits)
            {
                if ((string.Equals(pair.Key, normalizedCode, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(NormalizeLimitCode(pair.Key), normalizedCode, StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrWhiteSpace(pair.Value.Unit))
                {
                    return pair.Value.Unit;
                }
            }
        }

        return normalizedCode switch
        {
            "DCR" or "RS" => "mΩ",
            "Z" => "Ω",
            "LS" => "μH",
            "PHASE" => "°",
            "Q" or "D" => string.Empty,
            _ => null
        };
    }
    private IEnumerable<string> GetMachineCheckTestNames()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (TestStationModel station in machine.TestStations.Where(static station => station.ShowInResultGrid).Where(HasStationCheckOperation))
        {
            foreach (string testName in station.OrderedTestNames)
            {
                foreach (string name in SplitTestNames(testName))
                {
                    string code = NormalizeLimitCode(name) ?? name;
                    if (seen.Add(code))
                    {
                        yield return code;
                    }
                }
            }

            foreach (IStationInstrumentOperation operation in station.StationDataDeals.OfType<IStationInstrumentOperation>())
            {
                foreach (string name in SplitTestNames(operation.TestName))
                {
                    string code = NormalizeLimitCode(name) ?? name;
                    if (seen.Add(code))
                    {
                        yield return code;
                    }
                }
            }
        }
    }

    private static bool HasStationCheckOperation(TestStationModel station)
        => station.Operations.Any(operation => string.Equals(operation.Code, StationOperationDescriptor.Check, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitTestNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string item in value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return item;
        }
    }

    private static string? NormalizeLimitCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string code = value.Trim().ToUpperInvariant();
        while (code.Length > 0 && char.IsDigit(code[^1]))
        {
            code = code[..^1];
        }

        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    private static string FormatNumber(double? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string? GetParameterValue(MesParameterBag parameters, string key)
    {
        if (parameters.TryGetString(key, out string value))
        {
            return value;
        }

        MesParameterValue? parameter = parameters.Values
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;

        return parameter?.Value;
    }

    private async Task ResetStandardSampleExpiredSignalIfNeededAsync(StandardSamplePanelModel panel)
    {
        if (!ReferenceEquals(panel, StandardSample))
        {
            return;
        }

        try
        {
            await machine.SetStandardSampleExpiredAsync(false, DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // 查询成功不应被 PLC 复位失败阻断，设备层会记录硬件异常。
        }
    }

    private async Task WarnIfSampleExpiresSoonAsync(StandardSamplePanelModel panel)
    {
        if (string.IsNullOrWhiteSpace(panel.ExpireDate)
            || !DateTime.TryParse(panel.ExpireDate, out DateTime expireTime))
        {
            return;
        }

        TimeSpan remaining = expireTime - DateTime.Now;
        if (remaining >= TimeSpan.Zero && remaining <= TimeSpan.FromHours(24))
        {
            await notificationService.WarningAsync(localizationService.TF("Standard.Message.ExpiresWithin24Hours", "{0}到期时间：{1}，已不足24小时！", panel.SampleCode, panel.ExpireDate), GetPanelTitle(panel)).ConfigureAwait(true);
        }
    }

    private static bool IsMesAccepted<T>(MesResult<T> result)
        => result.Exchange?.ReturnCode is int returnCode ? returnCode == 0 : result.IsSuccess;

    private static void OnLimitItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 占位订阅，确保共享集合生命周期与 ViewModel 一致，后续需要集合级联刷新可在这里收敛。
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || disposed)
        {
            base.Dispose(disposing);
            return;
        }

        disposed = true;
        stationLimitsAppliedSubscription.Dispose();
        localizationService.LanguageChanged -= OnLanguageChanged;
        StandardSample.LimitItems.CollectionChanged -= OnLimitItemsChanged;
        ConfirmSample.LimitItems.CollectionChanged -= OnLimitItemsChanged;
        base.Dispose(disposing);
    }
}
