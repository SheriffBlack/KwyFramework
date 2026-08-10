using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Kwy.Files;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Instruments.Dcr;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;
using KwyTemplate.Flow.Services;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.Flow.Machines;

public class Machine_2_A :
    MachineBase,
    ICyntecReelScanMachine,
    IIndustrialPcOnlineSignalMachine,
    IMachinePlcStopSignalMachine,
    IMachineProductionCounterResetMachine,
    IMachineProductionSummaryMachine,
    IMachineBraidSetupMachine,
    IMachineWorkOrderStartSignalMachine
{
    private const string NullText = "(NULL)";
    private static readonly TimeSpan ParameterCompareResultDelay = TimeSpan.FromMilliseconds(2500);
    private readonly object testRecordSync = new();
    private readonly IProductionRuntimeContext? productionContext;
    private readonly IProductionOutputOptions? productionOutputOptions;
    private readonly IProductionRecordWriter? productionRecordWriter;
    private readonly MeasurementCpkAccumulator dcr1Cpk = new();
    private readonly MeasurementCpkAccumulator dcr2Cpk = new();
    private IMeasurementInstrument? dcrMeter1;
    private IMeasurementInstrument? dcrMeter2;
    private int parameterCompareWriteGate;
    private bool? previousParameterCompareSignal;
    private int systemDataReadGate;
    private MesWorkOrderTapeSetup? currentTapeSetup;
    private string? currentWorkOrderNo;
    private string? forceBraidSignalWrittenWorkOrderNo;
    private readonly Queue<Machine2ATestOutputRecord> pendingTestRecords = new();
    private Machine2AStationStatisticsSnapshot? lastDcr1StatisticsSnapshot;
    private Machine2AStationStatisticsSnapshot? lastDcr2StatisticsSnapshot;
    private static readonly IReadOnlyDictionary<string, MachineExamineFlowDescriptor> ExamineFlows = new Dictionary<string, MachineExamineFlowDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard"] = new(
            "Standard",
            Point("\u6807\u51c6\u4ef6"),
            Point("\u6807\u51c6\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u6807\u51c6\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u6807\u51c6\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u6807\u51c6\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 1, "DCR1", 5_000),
                new(Point("\u6807\u51c6\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u6807\u51c6\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 2, "DCR2", 10_000)
            ]),
        ["Confirm"] = new(
            "Confirm",
            Point("\u786e\u8ba4\u4ef6"),
            Point("\u786e\u8ba4\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u786e\u8ba4\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u786e\u8ba4\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u786e\u8ba4\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 1, "DCR1", 5_000),
                new(Point("\u786e\u8ba4\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u786e\u8ba4\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 2, "DCR2", 10_000)
            ])
    };
    private static int Point(string name)
        => (int)Enum.Parse<PlcPoints>(name);    public Machine_2_A(
        IMachineDeviceContext devices,
        IProductionRuntimeContext? productionContext = null,
        IProductionOutputOptions? productionOutputOptions = null,
        IProductionRecordWriter? productionRecordWriter = null,
        ILocalizationService? localizationService = null)
        : base(devices, localizationService)
    {
        this.productionContext = productionContext;
        this.productionOutputOptions = productionOutputOptions;
        this.productionRecordWriter = productionRecordWriter;
        BindDevices();
        InitTestStations();
        BuildDataGrid();
        RefreshStationLimitsFromInstrumentConfigs();
    }

    private static readonly IReadOnlySet<HomeWorkOrderField> MachineHomeWorkOrderFields =
        new HashSet<HomeWorkOrderField>(CreateDefaultHomeWorkOrderFields())
        {
            HomeWorkOrderField.ReelMatNo
        };

    public override TriggerMode StationTriggerMode => TriggerMode.Polling;

    public override IReadOnlySet<HomeWorkOrderField> HomeWorkOrderFields => MachineHomeWorkOrderFields;

    protected override bool ShouldApplyRealtimeStatisticsToTable => false;

    public int ReelScanInputChannel => (int)CardToPc.Reel扫;

    public enum CardToPc
    {
        [Description("参数对比")] 参数对比 = 0,
        [Description("DCR1测试完成")] DCR1测试完成 = 1,
        [Description("DCR2测试完成")] DCR2测试完成 = 2,
        [Description("备用3")] 备用3 = 3,
        [Description("备用4")] 备用4 = 4,
        [Description("备用5")] 备用5 = 5,
        [Description("备用6")] 备用6 = 6,
        [Description("备用7")] 备用7 = 7,
        [Description("DCR1 OK")] DCR1_OK = 8,
        [Description("DCR2 OK")] DCR2_OK = 9,
        [Description("备用10")] 备用10 = 10,
        [Description("备用11")] 备用11 = 11,
        [Description("备用12")] 备用12 = 12,
        [Description("备用13")] 备用13 = 13,
        [Description("备用14")] 备用14 = 14,
        [Description("Reel扫")] Reel扫 = 15
    }

    public enum PcToCard
    {
        [Description("工控机在线")] 工控机在线 = 0,
        [Description("DCR1读取完成")] DCR1读取完成 = 1,
        [Description("DCR2读取完成")] DCR2读取完成 = 2,
        [Description("备用3")] 备用3 = 3,
        [Description("备用4")] 备用4 = 4,
        [Description("备用5")] 备用5 = 5,
        [Description("备用6")] 备用6 = 6,
        [Description("备用7")] 备用7 = 7,
        [Description("扫工单")] 扫工单 = 8,
        [Description("参数对比 OK")] 参数对比_OK = 9,
        [Description("参数对比 NG")] 参数对比_NG = 10,
        [Description("MES清料")] MES清料 = 11,
        [Description("备用12")] 备用12 = 12,
        [Description("备用13")] 备用13 = 13,
        [Description("Reel扫OK")] Reel扫OK = 14,
        [Description("Reel扫NG")] Reel扫NG = 15
    }

    public enum PlcPoints
    {
        [Description("点检完成")][PlcPoint("DM7100", typeof(UInt16))] 点检完成,
        [Description("标准件过期")][PlcPoint("DM7102", typeof(UInt16))] 标准件过期,
        [Description("点检过期_一卷完成")][PlcPoint("DM7104", typeof(UInt16))] 点检过期_一卷完成,
        [Description("标准件过期_一卷完成")][PlcPoint("DM7106", typeof(UInt16))] 标准件过期_一卷完成,
        [Description("PC启动按钮")][PlcPoint("DM7200", typeof(UInt16))] PC启动按钮,
        [Description("PC停止按钮")][PlcPoint("DM7202", typeof(UInt16))] PC停止按钮,
        [Description("编带电机释放")][PlcPoint("DM7204", typeof(UInt16), IsReadOnly = true)] 编带电机释放,
        [Description("强制编带_新工单")][PlcPoint("DM7206", typeof(UInt16))] 强制编带_新工单,
        [Description("工位1开关")][PlcPoint("DM6100", typeof(UInt16))] 工位1开关,
        [Description("工位2开关")][PlcPoint("DM6102", typeof(UInt16))] 工位2开关,
        [Description("工位3开关")][PlcPoint("DM6104", typeof(UInt16))] 工位3开关,
        [Description("工位4开关")][PlcPoint("DM6106", typeof(UInt16))] 工位4开关,
        [Description("工位5开关")][PlcPoint("DM6108", typeof(UInt16))] 工位5开关,
        [Description("工位6开关")][PlcPoint("DM6110", typeof(UInt16))] 工位6开关,
        [Description("工位7开关")][PlcPoint("DM6112", typeof(UInt16))] 工位7开关,
        [Description("工位8开关")][PlcPoint("DM6114", typeof(UInt16))] 工位8开关,
        [Description("工位9开关")][PlcPoint("DM6116", typeof(UInt16))] 工位9开关,
        [Description("工位保存")][PlcPoint("DM6130", typeof(UInt16))] 工位保存,
        [Description("前空格")][PlcPoint("DM6204", typeof(UInt16))] 前空格,
        [Description("每卷包装数量")][PlcPoint("DM6202", typeof(UInt32))] 每卷包装数量,
        [Description("后空格")][PlcPoint("DM6206", typeof(UInt32))] 后空格,
        [Description("样品")][PlcPoint("DM6212", typeof(UInt32))] 样品,
        [Description("样品后空带")][PlcPoint("DM6210", typeof(UInt32))] 样品后空带,
        [Description("后不封")][PlcPoint("DM6208", typeof(UInt32))] 后不封,
        [Description("统计计数清零")][PlcPoint("DM6550", typeof(UInt16))] 统计计数清零,
        [Description("测试合格率")][PlcPoint("DM6500", typeof(float), IsReadOnly = true)] 测试合格率,
        [Description("测试总量")][PlcPoint("DM6502", typeof(UInt32), IsReadOnly = true)] 测试总量,
        [Description("测试OK数")][PlcPoint("DM6504", typeof(UInt32), IsReadOnly = true)] 测试OK数,
        [Description("测试NG数")][PlcPoint("DM6506", typeof(UInt32), IsReadOnly = true)] 测试NG数,
        [Description("DCR1_NG数")][PlcPoint("DM6508", typeof(UInt32), IsReadOnly = true)] DCR1_NG数,
        [Description("DCR2_NG数")][PlcPoint("DM6510", typeof(UInt32), IsReadOnly = true)] DCR2_NG数,
        [Description("DCR1_CE数")][PlcPoint("DM6524", typeof(UInt32), IsReadOnly = true)] DCR1_CE数,
        [Description("DCR2_CE数")][PlcPoint("DM6526", typeof(UInt32), IsReadOnly = true)] DCR2_CE数,
        [Description("A面相机NG数")][PlcPoint("DM6520", typeof(UInt32), IsReadOnly = true)] A面相机NG数,
        [Description("B面相机NG数")][PlcPoint("DM6522", typeof(UInt32), IsReadOnly = true)] B面相机NG数,
        [Description("启动点检条件满足")][PlcPoint("DM7000", typeof(UInt16), IsReadOnly = true)] 启动点检条件满足,
        [Description("进入点检界面")][PlcPoint("DM7002", typeof(UInt16))] 进入点检界面,
        [Description("标准件")][PlcPoint("DM7010", typeof(UInt16))] 标准件,
        [Description("标准件_点检启动")][PlcPoint("DM7011", typeof(UInt16))] 标准件_点检启动,
        [Description("标准件_PC触发DCR1仪器")][PlcPoint("DM7020", typeof(UInt16), IsReadOnly = true)] 标准件_PC触发DCR1仪器,
        [Description("标准件_PC读取DCR1数据完成")][PlcPoint("DM7012", typeof(UInt16))] 标准件_PC读取DCR1数据完成,
        [Description("标准件_PC触发DCR2仪器")][PlcPoint("DM7021", typeof(UInt16), IsReadOnly = true)] 标准件_PC触发DCR2仪器,
        [Description("标准件_PC读取DCR2数据完成")][PlcPoint("DM7013", typeof(UInt16))] 标准件_PC读取DCR2数据完成,
        [Description("标准件_点检完成")][PlcPoint("DM7022", typeof(UInt16))] 标准件_点检完成,
        [Description("确认件")][PlcPoint("DM7030", typeof(UInt16))] 确认件,
        [Description("确认件_点检启动")][PlcPoint("DM7031", typeof(UInt16))] 确认件_点检启动,
        [Description("确认件_PC触发DCR1仪器")][PlcPoint("DM7040", typeof(UInt16), IsReadOnly = true)] 确认件_PC触发DCR1仪器,
        [Description("确认件_PC读取DCR1数据完成")][PlcPoint("DM7032", typeof(UInt16))] 确认件_PC读取DCR1数据完成,
        [Description("确认件_PC触发DCR2仪器")][PlcPoint("DM7041", typeof(UInt16), IsReadOnly = true)] 确认件_PC触发DCR2仪器,
        [Description("确认件_PC读取DCR2数据完成")][PlcPoint("DM7033", typeof(UInt16))] 确认件_PC读取DCR2数据完成,
        [Description("确认件_点检完成")][PlcPoint("DM7042", typeof(UInt16))] 确认件_点检完成,
        [Description("强排盒计数")][PlcPoint("DM6532", typeof(UInt16), IsReadOnly = true)] 强排盒计数,
        [Description("补料盒计数")][PlcPoint("DM6534", typeof(UInt16), IsReadOnly = true)] 补料盒计数,
        [Description("是否在手动界面（>=200、<250）")][PlcPoint("EM0", typeof(UInt16), IsReadOnly = true)] 人机界面是否在手动,

    }

    public override void BindDevices()
    {
        RegisterAndCachePlcPoints<PlcPoints>();

        if (Devices.TryGet<IPlcDevice>(DeviceIds.MainPlc, out IPlcDevice? mainPlc))
        {
            BindPlc(mainPlc);
        }

        if (Devices.TryGet<IIoCardDevice>(DeviceIds.MainIoCard, out IIoCardDevice? mainIoCard) && mainIoCard != null)
        {
            BindIoCard(mainIoCard);
            RegisterCardToPcNames(mainIoCard);
            RegisterPcToCardNames(mainIoCard);
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Dcr", 1), out IMeasurementInstrument? dcr1))
        {
            dcrMeter1 = dcr1;
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Dcr", 2), out IMeasurementInstrument? dcr2))
        {
            dcrMeter2 = dcr2;
        }
    }

    private static void RegisterCardToPcNames(IIoCardDevice card)
    {
        foreach (CardToPc input in Enum.GetValues<CardToPc>())
        {
            card.SetDiName((int)input, GetDescription(input));
        }
    }

    private static void RegisterPcToCardNames(IIoCardDevice card)
    {
        foreach (PcToCard output in Enum.GetValues<PcToCard>())
        {
            card.SetDoName((int)output, GetDescription(output));
        }
    }

    public override void InitTestStations()
    {
        TestStations =
        [
            new TestStationModel
            {
                StationId = 1,
                StationName = "工位一 DCR1",
                StationNameKey = "Station.Machine2A.1.Name",
                StationShortNameKey = "Station.Common.1",
                StationDeviceNameKey = "Station.Device.DCR1",
                InstrumentDeviceIds = [DeviceIds.Instrument("Dcr", 1)],
                OrderedTestNames = ["DCR1"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["DCR1"] = 0 },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase) { ["DCR1"] = true },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = (int)CardToPc.DCR1测试完成,
                    ResultOkInput = (int)CardToPc.DCR1_OK,
                    ResultReadCompletedOutput = (int)PcToCard.DCR1读取完成
                },
                Operations = { new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" } },
                StationDataDeals = [new InstrumentMeasurementDataDeal("DCR1", dcrMeter1)]
            },
            new TestStationModel
            {
                StationId = 2,
                StationName = "工位二 DCR2",
                StationNameKey = "Station.Machine2A.2.Name",
                StationShortNameKey = "Station.Common.2",
                StationDeviceNameKey = "Station.Device.DCR2",
                InstrumentDeviceIds = [DeviceIds.Instrument("Dcr", 2)],
                OrderedTestNames = ["DCR2"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["DCR2"] = 0 },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase) { ["DCR2"] = true },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = (int)CardToPc.DCR2测试完成,
                    ResultOkInput = (int)CardToPc.DCR2_OK,
                    ResultReadCompletedOutput = (int)PcToCard.DCR2读取完成
                },
                Operations = { new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" } },
                StationDataDeals = [new InstrumentMeasurementDataDeal("DCR2", dcrMeter2)]
            },
            new TestStationModel
            {
                StationId = 3,
                StationSwitchPointKey = (int)PlcPoints.工位6开关,
                StationName = "工位三 A面相机",
                StationNameKey = "Station.Machine2A.3.Name",
                StationShortNameKey = "Station.Common.3",
                StationDeviceNameKey = "Station.Device.CameraA",
                ShowInResultGrid = false,
                OrderedTestNames = [],
                TestValues = new(StringComparer.OrdinalIgnoreCase),
                TestJudges = new(StringComparer.OrdinalIgnoreCase),
                StationDataDeals = []
            },
            new TestStationModel
            {
                StationId = 4,
                StationSwitchPointKey = (int)PlcPoints.工位8开关,
                StationName = "工位四 编带相机",
                StationNameKey = "Station.Machine2A.4.Name",
                StationShortNameKey = "Station.Common.4",
                StationDeviceNameKey = "Station.Device.TapingCamera",
                ShowInResultGrid = false,
                OrderedTestNames = [],
                TestValues = new(StringComparer.OrdinalIgnoreCase),
                TestJudges = new(StringComparer.OrdinalIgnoreCase),
                StationDataDeals = []
            }
        ];
    }

    #region 系统统计轮询

    protected override void ReadSystemData()
    {
        ReadParameterCompareSignal();

        if (productionContext?.IsResultGridDataEnabled != true)
        {
            lastDcr1StatisticsSnapshot = null;
            lastDcr2StatisticsSnapshot = null;
            return;
        }

        if (Interlocked.Exchange(ref systemDataReadGate, 1) == 1)
        {
            return;
        }

        _ = ReadSystemDataAsync();
    }

    private async Task ReadSystemDataAsync()
    {
        try
        {
            IPlcDevice? plc = Plc;
            if (plc is not { IsConnected: true })
            {
                return;
            }

            uint total = await ReadUInt32PointAsync(plc, PlcPoints.测试总量).ConfigureAwait(false);
            await TryUpdateStationStatisticsAsync(plc, 1, "DCR1", total, PlcPoints.DCR1_NG数, PlcPoints.DCR1_CE数).ConfigureAwait(false);
            await TryUpdateStationStatisticsAsync(plc, 2, "DCR2", total, PlcPoints.DCR2_NG数, PlcPoints.DCR2_CE数).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_2_A] Read system data failed: {ex}");
        }
        finally
        {
            Volatile.Write(ref systemDataReadGate, 0);
        }
    }

    private async Task TryUpdateStationStatisticsAsync(IPlcDevice plc, int stationId, string testName, uint total, PlcPoints ngPoint, PlcPoints cePoint)
    {
        try
        {
            uint ng = await ReadUInt32PointAsync(plc, ngPoint).ConfigureAwait(false);
            uint ce = await ReadUInt32PointAsync(plc, cePoint).ConfigureAwait(false);
            var snapshot = new Machine2AStationStatisticsSnapshot(total, ng, ce);

            if (stationId == 1)
            {
                if (snapshot.Equals(lastDcr1StatisticsSnapshot))
                {
                    return;
                }

                lastDcr1StatisticsSnapshot = snapshot;
            }
            else if (stationId == 2)
            {
                if (snapshot.Equals(lastDcr2StatisticsSnapshot))
                {
                    return;
                }

                lastDcr2StatisticsSnapshot = snapshot;
            }

            UpdateStationStatistics(stationId, testName, total, ng, ce);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_2_A] Read {testName} statistics failed. NgPoint={ngPoint}, CePoint={cePoint}, Message={ex.Message}");
        }
    }
    private void UpdateStationStatistics(int stationId, string testName, uint total, uint ng, uint ce)
    {
        TestStationModel? station = TestStations.FirstOrDefault(item => item.StationId == stationId);
        if (station == null)
        {
            return;
        }

        uint ok = total > ng + ce ? total - ng - ce : 0;
        double yield = total == 0 ? 0 : (double)ok / total;
        UpdateStatisticsRows(station, testName, total, ok, ng, yield);
    }

    private async Task<uint> ReadUInt32PointAsync(IPlcDevice plc, PlcPoints point)
    {
        string lowAddress = PlcAddressCache[(int)point];
        string highAddress = GetNextWordAddress(lowAddress);
        try
        {
            ushort low = unchecked((ushort)await plc.ReadInt16Async(lowAddress).ConfigureAwait(false));
            ushort high = unchecked((ushort)await plc.ReadInt16Async(highAddress).ConfigureAwait(false));
            return ((uint)high << 16) | low;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_2_A] Read PLC UInt32 failed. Point={point}, LowAddress={lowAddress}, HighAddress={highAddress}, Message={ex.Message}");
            throw;
        }
    }

    private static string GetNextWordAddress(string address)
    {
        int index = address.Length - 1;
        while (index >= 0 && char.IsDigit(address[index]))
        {
            index--;
        }

        if (index == address.Length - 1)
        {
            throw new InvalidOperationException($"PLC address does not contain a numeric suffix: {address}");
        }

        string prefix = address[..(index + 1)];
        string numberText = address[(index + 1)..];
        int next = int.Parse(numberText, CultureInfo.InvariantCulture) + 1;
        return prefix + next.ToString(CultureInfo.InvariantCulture);
    }
    private async Task<ushort> ReadUInt16PointAsync(IPlcDevice plc, PlcPoints point, CancellationToken cancellationToken)
    {
        short value = await plc.ReadInt16Async(PlcAddressCache[(int)point], cancellationToken).ConfigureAwait(false);
        return unchecked((ushort)value);
    }

    #endregion 系统统计轮询

    #region 参数对比

    private void ReadParameterCompareSignal()
    {
        if (!TryReadDiSnapshotBit((int)CardToPc.参数对比, out bool current))
        {
            return;
        }

        if (!previousParameterCompareSignal.HasValue)
        {
            previousParameterCompareSignal = current;
            return;
        }

        bool isRising = current && !previousParameterCompareSignal.Value;
        previousParameterCompareSignal = current;
        if (!isRising)
        {
            return;
        }

        if (Interlocked.Exchange(ref parameterCompareWriteGate, 1) == 1)
        {
            return;
        }

        _ = RewriteStationParametersAsync();
    }

    private async Task RewriteStationParametersAsync()
    {
        bool succeeded = false;
        try
        {
            ResetParameterCompareResultOutputs();

            foreach (string deviceId in TestStations.SelectMany(static station => station.InstrumentDeviceIds).Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Devices.TryGet<IConfigurableDevice>(deviceId, out IConfigurableDevice? device) && device != null)
                {
                    await device.ApplyConfigAsync().ConfigureAwait(false);
                }
            }

            await WriteTapeSetupToPlcAsync(currentTapeSetup, CancellationToken.None).ConfigureAwait(false);
            succeeded = true;

            // 人机在暂停态按启动时会触发参数对比；参数重写成功后，PC 侧恢复为 Running。
            ResumeProductionFromExternalSignal();
        }
        catch
        {
            // 设备层会按“参数写入/PLC 写入”规则记录失败日志，这里只负责给 IO 返回 NG。
        }
        finally
        {
            try
            {
                await PulseParameterCompareResultAsync(succeeded).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref parameterCompareWriteGate, 0);
            }
        }
    }

    private async Task PulseParameterCompareResultAsync(bool succeeded)
    {
        await Task.Delay(ParameterCompareResultDelay).ConfigureAwait(false);

        IIoCardDevice? ioCard = IoCard;
        if (ioCard is not { IsConnected: true })
        {
            return;
        }

        int channel = succeeded ? (int)PcToCard.参数对比_OK : (int)PcToCard.参数对比_NG;
        ResetParameterCompareResultOutputs(ioCard);
        ioCard.WriteDoBit(channel, true);
    }

    private void ResetParameterCompareResultOutputs()
    {
        IIoCardDevice? ioCard = IoCard;
        if (ioCard is { IsConnected: true })
        {
            ResetParameterCompareResultOutputs(ioCard);
        }
    }

    private static void ResetParameterCompareResultOutputs(IIoCardDevice ioCard)
    {
        ioCard.WriteDoBit((int)PcToCard.参数对比_OK, false);
        ioCard.WriteDoBit((int)PcToCard.参数对比_NG, false);
    }

    #endregion 参数对比

    public void SetCurrentWorkOrder(string? workOrderNo)
    {
        string? normalized = string.IsNullOrWhiteSpace(workOrderNo) ? null : workOrderNo.Trim();
        if (string.Equals(currentWorkOrderNo, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentWorkOrderNo = normalized;
    }

    public Task ResetWorkOrderStartSignalsAsync(CancellationToken cancellationToken = default)
        => WriteForceBraidNewWorkOrderSignalAsync(false, cancellationToken);
    public override async Task ApplyWorkOrderSetupAsync(MesWorkOrderSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        currentTapeSetup = setup.TapeSetup;
        await ApplyDcrInstrumentSetupAsync(setup.InstrumentSetups ?? [], cancellationToken).ConfigureAwait(false);
        RefreshStationLimitsFromInstrumentConfigs();
        await WriteTapeSetupToPlcAsync(currentTapeSetup, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyBraidSetupAsync(MesWorkOrderTapeSetup tapeSetup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tapeSetup);
        currentTapeSetup = tapeSetup;
        await WriteTapeSetupToPlcAsync(currentTapeSetup, cancellationToken).ConfigureAwait(false);
    }
    private async Task ApplyDcrInstrumentSetupAsync(IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups, CancellationToken cancellationToken)
    {
        MesWorkOrderInstrumentSetup? dcr1Setup = FindInstrumentSetup(instrumentSetups, "DCR1");
        MesWorkOrderInstrumentSetup? dcr2Setup = FindInstrumentSetup(instrumentSetups, "DCR2");
        SetStationTestLimit("DCR1", dcr1Setup?.LowerLimit, dcr1Setup?.UpperLimit, dcr1Setup?.Unit);
        SetStationTestLimit("DCR2", dcr2Setup?.LowerLimit, dcr2Setup?.UpperLimit, dcr2Setup?.Unit);
        await ApplyAdexDcrSetupAsync(dcrMeter1, dcr1Setup, cancellationToken).ConfigureAwait(false);
        await ApplyAdexDcrSetupAsync(dcrMeter2, dcr2Setup, cancellationToken).ConfigureAwait(false);
    }

    private static MesWorkOrderInstrumentSetup? FindInstrumentSetup(IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups, string parameterId)
        => instrumentSetups.FirstOrDefault(item => string.Equals(item.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase));

    private static async Task ApplyAdexDcrSetupAsync(IMeasurementInstrument? instrument, MesWorkOrderInstrumentSetup? setup, CancellationToken cancellationToken)
    {
        if (instrument == null || setup == null)
        {
            return;
        }

        if (instrument is not IConfigurableDevice configurable || configurable.DeviceParameter is not AdexDcrConfig config)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(setup.Range))
        {
            config.Range = NormalizeAdexRange(setup.Range);
        }
        string? unit = NormalizeAdexLimitUnit(setup.Unit);
        if (setup.LowerLimit.HasValue)
        {
            config.LowerLimitRaw = setup.LowerLimit.Value;
            if (!string.IsNullOrWhiteSpace(unit))
            {
                config.LowerLimitRawUnit = unit;
            }
        }

        if (setup.UpperLimit.HasValue)
        {
            config.UpperLimitRaw = setup.UpperLimit.Value;
            if (!string.IsNullOrWhiteSpace(unit))
            {
                config.UpperLimitRawUnit = unit;
            }
        }

        await configurable.ApplyConfigAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteTapeSetupToPlcAsync(MesWorkOrderTapeSetup? tapeSetup, CancellationToken cancellationToken)
    {
        if (tapeSetup == null)
        {
            return;
        }

        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            throw new InvalidOperationException("PLC is not connected. Cannot write tape setup.");
        }

        await WriteUInt16PointAsync(plc, PlcPoints.前空格, tapeSetup.BeforeSpaceQty, cancellationToken).ConfigureAwait(false);
        await WriteUInt32PointAsync(plc, PlcPoints.每卷包装数量, tapeSetup.PackageQty, cancellationToken).ConfigureAwait(false);
        await WriteUInt32PointAsync(plc, PlcPoints.后空格, tapeSetup.AfterSpaceQty, cancellationToken).ConfigureAwait(false);
        await WriteUInt32PointAsync(plc, PlcPoints.样品, tapeSetup.SampleQty, cancellationToken).ConfigureAwait(false);
        await WriteUInt32PointAsync(plc, PlcPoints.样品后空带, tapeSetup.BlankQty, cancellationToken).ConfigureAwait(false);
        await WriteUInt32PointAsync(plc, PlcPoints.后不封, tapeSetup.BackNoFilmQty, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteUInt16PointAsync(IPlcDevice plc, PlcPoints point, int? value, CancellationToken cancellationToken)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value is < short.MinValue or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Value, $"PLC point {point} value is out of Int16 range.");
        }

        await plc.WriteInt16Async(PlcAddressCache[(int)point], (short)value.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteUInt32PointAsync(IPlcDevice plc, PlcPoints point, int? value, CancellationToken cancellationToken)
    {
        if (!value.HasValue)
        {
            return;
        }

        await plc.WriteInt32Async(PlcAddressCache[(int)point], value.Value, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeAdexRange(string range)
    {
        string normalized = range.Trim();
        return normalized.Equals("1kΩ", StringComparison.OrdinalIgnoreCase) ? "1KΩ" : normalized;
    }

    private static string? NormalizeAdexLimitUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        string normalized = unit.Trim();
        if (normalized.Equals("uΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("μΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("uohm", StringComparison.OrdinalIgnoreCase))
        {
            return "μΩ";
        }

        if (normalized.Equals("mΩ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("mohm", StringComparison.OrdinalIgnoreCase))
        {
            return "mΩ";
        }

        if (normalized.Equals("Ω", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ohm", StringComparison.OrdinalIgnoreCase))
        {
            return "Ω";
        }

        return normalized;
    }
    private static int ConvertDcrLimitToAdexRaw(double value, string? unit, string mappedRange)
    {
        double normalizedValue = NormalizeDcrLimitValue(value, unit, mappedRange);
        double raw = mappedRange switch
        {
            "R0" => normalizedValue * 10000,
            "R1" => normalizedValue * 1000,
            "R2" => normalizedValue * 100,
            "R3" => normalizedValue * 10,
            "R4" => normalizedValue * 1000,
            "R5" => normalizedValue * 100,
            "R6" => normalizedValue * 10,
            _ => normalizedValue
        };

        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    private static double NormalizeDcrLimitValue(double value, string? unit, string mappedRange)
    {
        if (mappedRange is "R0" or "R1" or "R2" or "R3")
        {
            return !string.IsNullOrWhiteSpace(unit) && !unit.Contains("m", StringComparison.OrdinalIgnoreCase) && unit.Contains("Ω", StringComparison.OrdinalIgnoreCase)
                ? value * 1000
                : value;
        }

        return !string.IsNullOrWhiteSpace(unit) && unit.Contains("m", StringComparison.OrdinalIgnoreCase) ? value / 1000 : value;
    }

    public override async Task SetStationEnabledAsync(TestStationModel station, bool isEnabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            return;
        }

        int? stationSwitchPointKey = GetStationSwitchPointKey(station);
        if (!stationSwitchPointKey.HasValue)
        {
            return;
        }

        await plc.WriteInt16Async(PlcAddressCache[stationSwitchPointKey.Value], isEnabled ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
        await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.工位保存], 1, cancellationToken).ConfigureAwait(false);
        SetStationEnabledState(station, isEnabled);
    }

    public override async Task<bool?> ReadStationEnabledAsync(TestStationModel station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);

        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            return station.IsEnabled;
        }

        int? stationSwitchPointKey = GetStationSwitchPointKey(station);
        if (!stationSwitchPointKey.HasValue || !PlcAddressCache.TryGetValue(stationSwitchPointKey.Value, out string? address))
        {
            return station.IsEnabled;
        }

        short value = await plc.ReadInt16Async(address, cancellationToken).ConfigureAwait(false);
        return value != 0;
    }
    private static int? GetStationSwitchPointKey(TestStationModel station)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (station.StationSwitchPointKey.HasValue)
        {
            return station.StationSwitchPointKey.Value;
        }

        PlcPoints? point = GetStationSwitchPoint(station.StationId);
        return point.HasValue ? (int)point.Value : null;
    }

    private static PlcPoints? GetStationSwitchPoint(int stationId)
        => stationId switch
        {
            1 => PlcPoints.工位1开关,
            2 => PlcPoints.工位2开关,
            3 => PlcPoints.工位3开关,
            4 => PlcPoints.工位4开关,
            5 => PlcPoints.工位5开关,
            6 => PlcPoints.工位6开关,
            7 => PlcPoints.工位7开关,
            8 => PlcPoints.工位8开关,
            9 => PlcPoints.工位9开关,
            _ => null
        };

    public override async Task SetCheckViewActiveAsync(bool isActive, CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            return;
        }

        await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.进入点检界面], isActive ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SetCheckCompletedAsync(bool isCompleted, CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue(0, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, isCompleted ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SetStandardSampleExpiredAsync(bool isExpired, CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.标准件过期, out string? address))
        {
            return;
        }

        // 标准件过期点位为反向有效：过期写 0，查询成功/恢复有效写 1。
        await plc.WriteInt16Async(address, isExpired ? (short)0 : (short)1, cancellationToken).ConfigureAwait(false);
    }
    public Task<bool> SetIndustrialPcOnlineAsync(bool online, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IIoCardDevice? ioCard = IoCard;
        if (ioCard is not { IsConnected: true })
        {
            return Task.FromResult(false);
        }

        ioCard.WriteDoBit((int)PcToCard.工控机在线, online);
        return Task.FromResult(true);
    }

    public async Task<IReadOnlyList<MachinePlcStopSignal>> ReadPlcStopSignalsAsync(CancellationToken cancellationToken = default)
    {
        var signals = new List<MachinePlcStopSignal>();


        if (await IsPlcStopSignalActiveAsync(PlcPoints.点检过期_一卷完成, cancellationToken).ConfigureAwait(false))
        {
            signals.Add(new MachinePlcStopSignal(MachinePlcStopSignalKind.CheckExpiredReelCompleted, ClearTablePaperCode: false, ResetAfterHandled: false));
        }

        if (await IsPlcStopSignalActiveAsync(PlcPoints.标准件过期_一卷完成, cancellationToken).ConfigureAwait(false))
        {
            signals.Add(new MachinePlcStopSignal(MachinePlcStopSignalKind.StandardExpiredReelCompleted, ClearTablePaperCode: false, ResetAfterHandled: false));
        }

        return signals;
    }

    public async Task ResetPlcStopSignalAsync(MachinePlcStopSignalKind kind, CancellationToken cancellationToken = default)
    {
        PlcPoints point = kind switch
        {
            MachinePlcStopSignalKind.CheckExpiredReelCompleted => PlcPoints.点检过期_一卷完成,
            MachinePlcStopSignalKind.StandardExpiredReelCompleted => PlcPoints.标准件过期_一卷完成,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        await WritePlcStopSignalAsync(point, 0, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCheckStopSignalsCompletedAsync(CancellationToken cancellationToken = default)
    {
        // These are completion acknowledgements, not reset commands. The PLC
        // receives both expired-check reel-completed flags after a successful save.
        await WritePlcStopSignalAsync(PlcPoints.点检过期_一卷完成, 1, cancellationToken).ConfigureAwait(false);
        await WritePlcStopSignalAsync(PlcPoints.标准件过期_一卷完成, 1, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsPlcStopSignalActiveAsync(PlcPoints point, CancellationToken cancellationToken)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)point, out string? address))
        {
            return false;
        }

        short value = await plc.ReadInt16Async(address, cancellationToken).ConfigureAwait(false);
        return value == 1;
    }

    private async Task WritePlcStopSignalAsync(PlcPoints point, short value, CancellationToken cancellationToken)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)point, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, value, cancellationToken).ConfigureAwait(false);
    }

    #region 生产记录

    protected override Task ProcessTestRecordAsync(TestResultPayload record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (record.Type != RecordType.Numeric)
        {
            return Task.CompletedTask;
        }

        Machine2ATestOutputRecord? completedRecord = null;
        lock (testRecordSync)
        {
            if (string.Equals(record.Name, "DCR1", StringComparison.OrdinalIgnoreCase))
            {
                dcr1Cpk.Add(record.TestValue);
                Machine2ATestOutputRecord outputRecord = new() { Dcr1 = record.TestValue, Dcr1Result = record.Judge, Dcr1TestTime = DateTime.Now };
                if (record.Judge == false)
                {
                    completedRecord = outputRecord;
                }
                else
                {
                    pendingTestRecords.Enqueue(outputRecord);
                }
            }
            else if (string.Equals(record.Name, "DCR2", StringComparison.OrdinalIgnoreCase))
            {
                dcr2Cpk.Add(record.TestValue);
                Machine2ATestOutputRecord outputRecord = pendingTestRecords.Count > 0
                    ? pendingTestRecords.Dequeue()
                    : new Machine2ATestOutputRecord();
                outputRecord.Dcr2 = record.TestValue;
                outputRecord.Dcr2Result = record.Judge;
                outputRecord.Dcr2TestTime = DateTime.Now;
                completedRecord = outputRecord;
            }
        }

        if (completedRecord.HasValue)
        {
            EnqueueTestOutputRecord(completedRecord.Value);
        }

        return Task.CompletedTask;
    }

    private void EnqueueTestOutputRecord(Machine2ATestOutputRecord record)
    {
        IProductionRecordWriter writer = productionRecordWriter ?? throw new InvalidOperationException("Production record writer is not configured.");
        string directory = ProductionRecordPathHelper.RuntimeDirectory;
        string fileName = ProductionRecordPathHelper.BuildFileName(productionContext?.WorkOrderNo, MachineId);
        string[] fields =
        [
            FormatNullableValue(record.Dcr1),
            FormatNullableJudge(record.Dcr1Result),
            FormatNullableTime(record.Dcr1TestTime),
            FormatNullableValue(record.Dcr2),
            FormatNullableJudge(record.Dcr2Result),
            FormatNullableTime(record.Dcr2TestTime),
            NullText,
            EscapeCsv(productionContext?.OperatorNo)
        ];
        if (!writer.TryEnqueue(new ProductionRecordWriteRequest(directory, fileName, fields)))
        {
            throw new InvalidOperationException($"Failed to enqueue production record: {Path.Combine(directory, fileName)}");
        }
    }

    private static string FormatNullableValue(double? value) => value.HasValue ? value.Value.ToString("F4", CultureInfo.InvariantCulture) : NullText;
    private static string FormatNullableJudge(bool? judge) => judge switch { true => "OK", false => "NG", _ => NullText };
    private static string FormatNullableTime(DateTime? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) : NullText;

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    #endregion 生产记录

    #region 汇总文件

    public async Task SaveProductionSummaryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            throw new InvalidOperationException("PLC is not connected. Cannot save production summary.");
        }

        string directory = string.IsNullOrWhiteSpace(productionOutputOptions?.SummaryDirectory) ? @"D:\MES\Summary" : productionOutputOptions.SummaryDirectory;
        string fileName = ProductionRecordPathHelper.BuildFileName(productionContext?.WorkOrderNo, MachineId);

        uint outputQty = await ReadUInt32PointAsync(plc, PlcPoints.测试总量).ConfigureAwait(false);
        float yield = await plc.ReadFloatAsync(PlcAddressCache[(int)PlcPoints.测试合格率], cancellationToken).ConfigureAwait(false);
        uint ngSum = await ReadUInt32PointAsync(plc, PlcPoints.测试NG数).ConfigureAwait(false);
        ushort tcAddQty = await ReadUInt16PointAsync(plc, PlcPoints.补料盒计数, cancellationToken).ConfigureAwait(false);
        double? dcr1CpkValue = CalculateCpk("DCR1", dcr1Cpk);
        double? dcr2CpkValue = CalculateCpk("DCR2", dcr2Cpk);

        string[] lines = BuildProductionSummaryLines(outputQty, yield, ngSum, tcAddQty, dcr1CpkValue, dcr2CpkValue);

        await TextFileHelper.WriteAsync(directory, fileName, string.Join(Environment.NewLine, lines), appendNewLine: false).ConfigureAwait(false);
    }

    private string[] BuildProductionSummaryLines(
        uint outputQty,
        float yield,
        uint ngSum,
        ushort tcAddQty,
        double? dcr1CpkValue,
        double? dcr2CpkValue)
        =>
        [
            BuildSummaryLine("OutputQty", outputQty.ToString(CultureInfo.InvariantCulture)),
            BuildSummaryLine("Yeld", FormatPercent(yield)),
            BuildSummaryLine("NGSum", ngSum.ToString(CultureInfo.InvariantCulture)),
            BuildSummaryLine("TCAddQty", tcAddQty.ToString(CultureInfo.InvariantCulture)),
            BuildSummaryLine("LeaveQty", "0"),
            BuildSummaryLine("Remark", (currentTapeSetup?.SampleQty ?? 0).ToString(CultureInfo.InvariantCulture)),
            BuildSummaryLine("AdjustQty", "0"),
            BuildSummaryLine("NGPOLAR1", "0"),
            BuildSummaryLine("NGPOLAR2", "0"),
            BuildSummaryLine("CpkDCR1", FormatNullableCpk(dcr1CpkValue)),
            BuildSummaryLine("CpkDCR2", FormatNullableCpk(dcr2CpkValue))
        ];
    // D:\MES is an external MES interface. Keep its file payload in the
    // original, untranslated key/value format used by the legacy machine.
    private static string BuildSummaryLine(string code, string value)
        => string.Join(',', code, value);
    private static string FormatPercent(float value)
    {
        double percent = Math.Abs(value) <= 1 ? value * 100 : value;
        return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    private double? CalculateCpk(string testName, MeasurementCpkAccumulator accumulator)
    {
        StationMeasurementLimit? limit = TestStations
            .Select(station => station.TestLimits.TryGetValue(testName, out StationMeasurementLimit? item) ? item : null)
            .FirstOrDefault(item => item?.LowerLimit.HasValue == true && item.UpperLimit.HasValue);

        return limit?.LowerLimit == null || limit.UpperLimit == null
            ? null
            : accumulator.CalculateCpk(limit.LowerLimit.Value, limit.UpperLimit.Value);
    }

    private static string FormatNullableCpk(double? value)
        => value.HasValue ? value.Value.ToString("0.####", CultureInfo.InvariantCulture) : string.Empty;

    private void ResetRunSummaryState()
    {
        dcr1Cpk.Reset();
        dcr2Cpk.Reset();
        pendingTestRecords.Clear();
    }

    #endregion 汇总文件

    public override Task<MachineExamineResult> ExecuteExamineAsync(string flowCode, IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
        => ExamineFlows.TryGetValue(flowCode, out MachineExamineFlowDescriptor? flow)
            ? ExecuteExamineFlowAsync(flow, progress, cancellationToken)
            : base.ExecuteExamineAsync(flowCode, progress, cancellationToken);
    protected override async Task OnTestStartedAsync(CancellationToken cancellationToken)
    {
        ResetRunSummaryState();
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true })
        {
            return;
        }

        if (ShouldWriteForceBraidNewWorkOrderSignal())
        {
            await WriteForceBraidNewWorkOrderSignalAsync(true, cancellationToken).ConfigureAwait(false);
            forceBraidSignalWrittenWorkOrderNo = currentWorkOrderNo;
        }
        await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.PC启动按钮], 1, cancellationToken).ConfigureAwait(false);
        await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.PC停止按钮], 0, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task OnTestStoppedAsync(CancellationToken cancellationToken)
    {
        try
        {
            IPlcDevice? plc = Plc;
            if (plc is { IsConnected: true })
            {
                await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.PC启动按钮], 0, cancellationToken).ConfigureAwait(false);
                await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.PC停止按钮], 1, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await FlushProductionRecordsAsync().ConfigureAwait(false);
        }
    }

    private bool ShouldWriteForceBraidNewWorkOrderSignal()
    {
        return !string.IsNullOrWhiteSpace(currentWorkOrderNo)
            && !string.Equals(forceBraidSignalWrittenWorkOrderNo, currentWorkOrderNo, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteForceBraidNewWorkOrderSignalAsync(bool value, CancellationToken cancellationToken)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.强制编带_新工单, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, value ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }
    public async Task<bool> ResetProductionCounterAsync(CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.统计计数清零, out string? address))
        {
            return false;
        }

        await plc.WriteInt16Async(address, 1, cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected override async Task OnTestPausedAsync(CancellationToken cancellationToken)
    {
        try
        {
            IPlcDevice? plc = Plc;
            if (plc is { IsConnected: true })
            {
                await plc.WriteInt16Async(PlcAddressCache[(int)PlcPoints.PC停止按钮], 1, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await FlushProductionRecordsAsync().ConfigureAwait(false);
        }
    }

    private async Task FlushProductionRecordsAsync()
    {
        if (productionRecordWriter == null)
        {
            return;
        }

        await productionRecordWriter.FlushAsync().ConfigureAwait(false);
    }
}

internal sealed record Machine2AStationStatisticsSnapshot(uint Total, uint Ng, uint Ce);

internal struct Machine2ATestOutputRecord
{
    public double? Dcr1 { get; set; }
    public bool? Dcr1Result { get; set; }
    public DateTime? Dcr1TestTime { get; set; }
    public double? Dcr2 { get; set; }
    public bool? Dcr2Result { get; set; }
    public DateTime? Dcr2TestTime { get; set; }
}








