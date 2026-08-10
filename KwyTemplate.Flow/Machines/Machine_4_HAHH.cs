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
using Kwy.Device.Instruments.Lcr;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.MarkPrinters;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;
using KwyTemplate.Flow.Services;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.Flow.Machines;
public class Machine_4_HAHH : 
    MachineBase, 
    ICyntecReelScanMachine, 
    IIndustrialPcOnlineSignalMachine, 
    IMachinePlcStopSignalMachine,
    IMachineProductionCounterResetMachine,
    IMachineProductionSummaryMachine,
    IMachineBraidSetupMachine,
    IMachineMarkPrintOptionsMachine,
    IMachineWorkOrderStartSignalMachine
{
    private const string NullText = "(NULL)";
    private static readonly TimeSpan ParameterCompareResultDelay = TimeSpan.FromMilliseconds(2500);
    private readonly IProductionRuntimeContext? productionContext;
    private readonly IProductionOutputOptions? productionOutputOptions;
    private readonly IProductionRecordWriter? productionRecordWriter;
    private readonly MeasurementCpkAccumulator dcr1Cpk = new();
    private readonly MeasurementCpkAccumulator lsCpk = new();
    private readonly MeasurementCpkAccumulator rsCpk = new();
    private readonly Queue<Machine4HahhTestOutputRecord> pendingTestRecords = new();
    private IMeasurementInstrument? polMeter1;
    private IMeasurementInstrument? polMeter2;
    private IMeasurementInstrument? dcrMeter1;
    private IMeasurementInstrument? indMeter1;
    private IMarkPrintDevice? markPrintDevice;
    private int parameterCompareWriteGate;
    private bool? previousParameterCompareSignal;
    private int systemDataReadGate;
    private MesWorkOrderTapeSetup? currentTapeSetup;
    private string? currentWorkOrderNo;
    private string? forceBraidSignalWrittenWorkOrderNo;
    private readonly Dictionary<string, Machine4HahhStationStatisticsSnapshot> lastStatisticsSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, MachineExamineFlowDescriptor> ExamineFlows = new Dictionary<string, MachineExamineFlowDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard"] = new(
            "Standard",
            Point("\u6807\u51c6\u4ef6"),
            Point("\u6807\u51c6\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u6807\u51c6\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u6807\u51c6\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u6807\u51c6\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 3, "DCR1", 5_000),
                new(Point("\u6807\u51c6\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u6807\u51c6\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 4, "Ls", 10_000)
            ]),
        ["Confirm"] = new(
            "Confirm",
            Point("\u786e\u8ba4\u4ef6"),
            Point("\u786e\u8ba4\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u786e\u8ba4\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u786e\u8ba4\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u786e\u8ba4\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 3, "DCR1", 5_000),
                new(Point("\u786e\u8ba4\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u786e\u8ba4\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 4, "Ls", 10_000)
            ]),
        ["PolarityForward"] = new(
            "PolarityForward",
            Point("\u6781\u6027\u6b63\u5411\u4ef6"),
            Point("\u6781\u6027\u6b63\u5411\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u6781\u6027\u6b63\u5411\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u6781\u6027\u6b63\u5411\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u6781\u6027\u6b63\u5411\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 1, "PHASE", 10_000),
                new(Point("\u6781\u6027\u6b63\u5411\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u6781\u6027\u6b63\u5411\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 2, "PHASE", 10_000)
            ],
            RepeatCount: 10),
        ["PolarityReverse"] = new(
            "PolarityReverse",
            Point("\u6781\u6027\u53cd\u5411\u4ef6"),
            Point("\u6781\u6027\u53cd\u5411\u4ef6_\u70b9\u68c0\u542f\u52a8"),
            Point("\u6781\u6027\u53cd\u5411\u4ef6_\u70b9\u68c0\u5b8c\u6210"),
            [
                new(Point("\u6781\u6027\u53cd\u5411\u4ef6_PC\u89e6\u53d1DCR1\u4eea\u5668"), Point("\u6781\u6027\u53cd\u5411\u4ef6_PC\u8bfb\u53d6DCR1\u6570\u636e\u5b8c\u6210"), 1, "PHASE", 10_000),
                new(Point("\u6781\u6027\u53cd\u5411\u4ef6_PC\u89e6\u53d1DCR2\u4eea\u5668"), Point("\u6781\u6027\u53cd\u5411\u4ef6_PC\u8bfb\u53d6DCR2\u6570\u636e\u5b8c\u6210"), 2, "PHASE", 10_000)
            ],
            RepeatCount: 10)
    };
    private static int Point(string name)
        => (int)Enum.Parse<PlcPoints>(name);

    public Machine_4_HAHH(
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

    public override TriggerMode StationTriggerMode => TriggerMode.Polling;

    protected override bool ShouldApplyRealtimeStatisticsToTable => false;

    public int ReelScanInputChannel => (int)CardToPc.Reel扫;

    public enum CardToPc
    {
        [Description("参数对比")] 参数对比 = 0,
        [Description("POL1测试完成")] POL1测试完成 = 1,
        [Description("POL2测试完成")] POL2测试完成 = 2,
        [Description("DCR1测试完成")] DCR1测试完成 = 3,
        [Description("IND1测试完成")] IND1测试完成 = 4,
        [Description("编带相机完成")] 编带相机完成 = 5,
        [Description("A面相机完成")] A面相机完成 = 6,
        [Description("B面相机完成")] B面相机完成 = 7,
        [Description("POL1 OK")] POL1_OK = 8,
        [Description("POL2 OK")] POL2_OK = 9,
        [Description("DCR1 OK")] DCR1_OK = 10,
        [Description("IND1 OK")] IND1_OK = 11,
        [Description("编带相机 OK")] 编带相机_OK = 12,
        [Description("A面相机 OK")] A面相机_OK = 13,
        [Description("B面相机 OK")] B面相机_OK = 14,
        [Description("Reel扫")] Reel扫 = 15
    }

    public enum PcToCard
    {
        [Description("工控机在线")] 工控机在线 = 0,
        [Description("POL1读取完成")] POL1读取完成 = 1,
        [Description("POL2读取完成")] POL2读取完成 = 2,
        [Description("DCR1读取完成")] DCR1读取完成 = 3,
        [Description("IND1读取完成")] IND1读取完成 = 4,
        [Description("编带相机读取完成")] 编带相机读取完成 = 5,
        [Description("A面相机读取完成")] A面相机读取完成 = 6,
        [Description("B面相机读取完成")] B面相机读取完成 = 7,
        [Description("扫工单")] 扫工单 = 8,
        [Description("参数对比 OK")] 参数对比_OK = 9,
        [Description("参数对比 NG")] 参数对比_NG = 10,
        [Description("MES清料")] MES清料 = 11,
        [Description("Ls OK")] Ls_OK = 12,
        [Description("Rs OK")] Rs_OK = 13,
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
        [Description("样品后空格")][PlcPoint("DM6210", typeof(UInt32))] 样品后空格,
        [Description("后不封")][PlcPoint("DM6208", typeof(UInt32))] 后不封,
        [Description("统计计数清零")][PlcPoint("DM6550", typeof(UInt16))] 统计计数清零,
        [Description("测试合格率")][PlcPoint("DM6500", typeof(float), IsReadOnly = true)] 测试合格率,
        [Description("测试总量")][PlcPoint("DM6502", typeof(UInt32), IsReadOnly = true)] 测试总量,
        [Description("测试OK数")][PlcPoint("DM6504", typeof(UInt32), IsReadOnly = true)] 测试OK数,
        [Description("测试NG数")][PlcPoint("DM6506", typeof(UInt32), IsReadOnly = true)] 测试NG数,

        [Description("POL1_NG数")][PlcPoint("DM6508", typeof(UInt32), IsReadOnly = true)] POL1_NG数,
        [Description("POL2_NG数")][PlcPoint("DM6510", typeof(UInt32), IsReadOnly = true)] POL2_NG数,
        [Description("DCR1_NG数")][PlcPoint("DM6512", typeof(UInt32), IsReadOnly = true)] DCR1_NG数,
        [Description("DCR1_CE数")][PlcPoint("DM6514", typeof(UInt32), IsReadOnly = true)] DCR1_CE数,
        [Description("LS_NG数")][PlcPoint("DM6516", typeof(UInt32), IsReadOnly = true)] LS_NG数,
        [Description("RS_NG数")][PlcPoint("DM6518", typeof(UInt32), IsReadOnly = true)] RS_NG数,
        [Description("A面相机NG数")][PlcPoint("DM6520", typeof(UInt32), IsReadOnly = true)] A面相机NG数,
        [Description("B面相机NG数")][PlcPoint("DM6522", typeof(UInt32), IsReadOnly = true)] B面相机NG数,
        [Description("POL1_CE数")][PlcPoint("DM6524", typeof(UInt32), IsReadOnly = true)] POL1_CE数,
        [Description("POL2_CE数")][PlcPoint("DM6526", typeof(UInt32), IsReadOnly = true)] POL2_CE数,
        [Description("IND_CE")][PlcPoint("DM6528", typeof(UInt32), IsReadOnly = true)] IND_CE,

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

        [Description("极性正向件")][PlcPoint("DM7050", typeof(UInt16))] 极性正向件,
        [Description("极性正向件_点检启动")][PlcPoint("DM7051", typeof(UInt16))] 极性正向件_点检启动,
        [Description("极性正向件_PC触发DCR1仪器")][PlcPoint("DM7060", typeof(UInt16), IsReadOnly = true)] 极性正向件_PC触发DCR1仪器,
        [Description("极性正向件_PC读取DCR1数据完成")][PlcPoint("DM7052", typeof(UInt16))] 极性正向件_PC读取DCR1数据完成,
        [Description("极性正向件_PC触发DCR2仪器")][PlcPoint("DM7061", typeof(UInt16), IsReadOnly = true)] 极性正向件_PC触发DCR2仪器,
        [Description("极性正向件_PC读取DCR2数据完成")][PlcPoint("DM7053", typeof(UInt16))] 极性正向件_PC读取DCR2数据完成,
        [Description("极性正向件_点检完成")][PlcPoint("DM7062", typeof(UInt16))] 极性正向件_点检完成,

        [Description("极性反向件")][PlcPoint("DM7070", typeof(UInt16))] 极性反向件,
        [Description("极性反向件_点检启动")][PlcPoint("DM7071", typeof(UInt16))] 极性反向件_点检启动,
        [Description("极性反向件_PC触发DCR1仪器")][PlcPoint("DM7080", typeof(UInt16), IsReadOnly = true)] 极性反向件_PC触发DCR1仪器,
        [Description("极性反向件_PC读取DCR1数据完成")][PlcPoint("DM7072", typeof(UInt16))] 极性反向件_PC读取DCR1数据完成,
        [Description("极性反向件_PC触发DCR2仪器")][PlcPoint("DM7081", typeof(UInt16), IsReadOnly = true)] 极性反向件_PC触发DCR2仪器,
        [Description("极性反向件_PC读取DCR2数据完成")][PlcPoint("DM7073", typeof(UInt16))] 极性反向件_PC读取DCR2数据完成,
        [Description("极性反向件_点检完成")][PlcPoint("DM7082", typeof(UInt16))] 极性反向件_点检完成,

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


        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Pol", 1), out IMeasurementInstrument? pol1))
        {
            polMeter1 = pol1;
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Pol", 2), out IMeasurementInstrument? pol2))
        {
            polMeter2 = pol2;
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Dcr", 1), out IMeasurementInstrument? dcr1))
        {
            dcrMeter1 = dcr1;
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Ind", 1), out IMeasurementInstrument? ind1))
        {
            indMeter1 = ind1;
        }

        if (Devices.TryGet<IMarkPrintDevice>(DeviceIds.MainMarkPrinter, out IMarkPrintDevice? markPrinter))
        {
            markPrintDevice = markPrinter;
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
                StationName = "工位一",
                StationNameKey = "Station.Machine4HAHH.1.Name",
                StationShortNameKey = "Station.Common.1",
                StationDeviceNameKey = "Station.Device.ZPhase",
                InstrumentDeviceIds = [DeviceIds.Instrument("Pol", 1)],

                /// 不需要显示到 DataGrid
                OrderedTestNames = [],
                ShowInResultGrid = false,
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = 1,
                    ResultOkInput = 8,
                    ResultReadCompletedOutput = 1
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" }
                },
                StationDataDeals = [new InstrumentMultiMeasurementDataDeal(polMeter1)]
            },

            new TestStationModel
            {
                StationId = 2,
                StationName = "工位二",
                StationNameKey = "Station.Machine4HAHH.2.Name",
                StationShortNameKey = "Station.Common.2",
                StationDeviceNameKey = "Station.Device.ZPhase",
                InstrumentDeviceIds = [DeviceIds.Instrument("Pol", 2)],
                OrderedTestNames = [],
                ShowInResultGrid = false,
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = 2,
                    ResultOkInput = 9,
                    ResultReadCompletedOutput = 2
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" }
                },
                StationDataDeals = [new InstrumentMultiMeasurementDataDeal(polMeter2)]
            },

            new TestStationModel
            {
                StationId = 3,
                StationName = "工位三",
                StationNameKey = "Station.Machine4HAHH.3.Name",
                StationShortNameKey = "Station.Common.3",
                StationDeviceNameKey = "Station.Device.DCR1",
                InstrumentDeviceIds = [DeviceIds.Instrument("Dcr", 1)],
                OrderedTestNames = ["DCR1"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DCR1"] = 0
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DCR1"] = true
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = 3,
                    ResultOkInput = 10,
                    ResultReadCompletedOutput = 3
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" }
                },
                StationDataDeals = [new InstrumentMeasurementDataDeal("DCR1", dcrMeter1)]
            },

            new TestStationModel
            {
                StationId = 4,
                StationName = "工位四",
                StationNameKey = "Station.Machine4HAHH.4.Name",
                StationShortNameKey = "Station.Common.4",
                StationDeviceNameKey = "Station.Device.LsRs",
                InstrumentDeviceIds = [DeviceIds.Instrument("Ind", 1)],
                OrderedTestNames = [],
                UseInstrumentConfigTestNames = true,
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = 4,
                    ResultSource = StationResultSource.Software,
                    ResultReadCompletedOutput = 4
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" },
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Calibration, DisplayName = "校正" }
                },
                StationDataDeals = [new InstrumentMultiMeasurementDataDeal(indMeter1)]
            },
            new TestStationModel
            {
                StationId = 5,
                StationSwitchPointKey = (int)PlcPoints.工位6开关,
                StationName = "工位五 A面相机",
                StationNameKey = "Station.Machine4HAHH.5.Name",
                StationShortNameKey = "Station.Common.5",
                StationDeviceNameKey = "Station.Device.CameraA",
                ShowInResultGrid = false,
                OrderedTestNames = [],
                TestValues = new(StringComparer.OrdinalIgnoreCase),
                TestJudges = new(StringComparer.OrdinalIgnoreCase),
                StationDataDeals = []
            },
            new TestStationModel
            {
                StationId = 6,
                StationSwitchPointKey = (int)PlcPoints.工位7开关,
                StationName = "工位六 B面相机",
                StationNameKey = "Station.Machine4HAHH.6.Name",
                StationShortNameKey = "Station.Common.6",
                StationDeviceNameKey = "Station.Device.CameraB",
                ShowInResultGrid = false,
                OrderedTestNames = [],
                TestValues = new(StringComparer.OrdinalIgnoreCase),
                TestJudges = new(StringComparer.OrdinalIgnoreCase),
                StationDataDeals = []
            },
            new TestStationModel
            {
                StationId = 7,
                StationSwitchPointKey = (int)PlcPoints.工位8开关,
                StationName = "工位七 编带相机",
                StationNameKey = "Station.Machine4HAHH.7.Name",
                StationShortNameKey = "Station.Common.7",
                StationDeviceNameKey = "Station.Device.TapingCamera",
                ShowInResultGrid = false,
                OrderedTestNames = [],
                TestValues = new(StringComparer.OrdinalIgnoreCase),
                TestJudges = new(StringComparer.OrdinalIgnoreCase),
                StationDataDeals = []
            }
        ];
    }

    public override Task CompleteStationHandshakeAsync(TestStationModel station, bool isPass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);

        if (station.StationId == 4)
        {
            WriteInductanceResultOutputs(station);
        }

        return base.CompleteStationHandshakeAsync(station, isPass, cancellationToken);
    }

    private void WriteInductanceResultOutputs(TestStationModel station)
    {
        if (IoCard is not { IsConnected: true })
        {
            return;
        }

        WriteDoBitSafe((int)PcToCard.Ls_OK, false);
        WriteDoBitSafe((int)PcToCard.Rs_OK, false);

        WriteDoBitSafe((int)PcToCard.Ls_OK, station.TestJudges.TryGetValue("Ls", out bool lsOk) && lsOk);
        WriteDoBitSafe((int)PcToCard.Rs_OK, station.TestJudges.TryGetValue("Rs", out bool rsOk) && rsOk);
    }

    private void WriteDoBitSafe(int channel, bool value)
    {
        try
        {
            if (IoCard is { IsConnected: true })
            {
                IoCard.WriteDoBit(channel, value);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_4_HAHH] Write DO failed. Channel={channel}, Value={value}, Message={ex.Message}");
        }
    }
    #region 系统统计轮询

    protected override void ReadSystemData()
    {
        ReadParameterCompareSignal();

        if (productionContext?.IsResultGridDataEnabled != true)
        {
            lastStatisticsSnapshots.Clear();
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
            await TryUpdateStationStatisticsAsync(plc, 3, "DCR1", total, PlcPoints.DCR1_NG数, PlcPoints.DCR1_CE数).ConfigureAwait(false);
            await TryUpdateStationStatisticsAsync(plc, 4, "Ls", total, PlcPoints.LS_NG数, PlcPoints.IND_CE).ConfigureAwait(false);
            await TryUpdateStationStatisticsAsync(plc, 4, "Rs", total, PlcPoints.RS_NG数, PlcPoints.IND_CE).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_4_HAHH] Read system data failed: {ex}");
        }
        finally
        {
            Volatile.Write(ref systemDataReadGate, 0);
        }
    }

    private async Task TryUpdateStationStatisticsAsync(IPlcDevice plc, int stationId, string testName, uint total, PlcPoints ngPoint, PlcPoints? cePoint = null)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return;
        }

        try
        {
            uint ng = await ReadUInt32PointAsync(plc, ngPoint).ConfigureAwait(false);
            uint ce = cePoint.HasValue ? await ReadUInt32PointAsync(plc, cePoint.Value).ConfigureAwait(false) : 0;
            var snapshot = new Machine4HahhStationStatisticsSnapshot(total, ng, ce);

            if (lastStatisticsSnapshots.TryGetValue(testName, out Machine4HahhStationStatisticsSnapshot? lastSnapshot)
                && snapshot.Equals(lastSnapshot))
            {
                return;
            }

            lastStatisticsSnapshots[testName] = snapshot;
            UpdateStationStatistics(stationId, testName, total, ng, ce);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Machine_4_HAHH] Read station statistics failed. StationId={stationId}, TestName={testName}, NgPoint={ngPoint}, CePoint={cePoint}, Message={ex.Message}");
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
        ushort low = unchecked((ushort)await plc.ReadInt16Async(lowAddress).ConfigureAwait(false));
        ushort high = unchecked((ushort)await plc.ReadInt16Async(highAddress).ConfigureAwait(false));
        return ((uint)high << 16) | low;
    }

    private async Task<ushort> ReadUInt16PointAsync(IPlcDevice plc, PlcPoints point, CancellationToken cancellationToken)
    {
        short value = await plc.ReadInt16Async(PlcAddressCache[(int)point], cancellationToken).ConfigureAwait(false);
        return unchecked((ushort)value);
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
            ResumeProductionFromExternalSignal();
        }
        catch
        {
            // 设备层会按参数写入/PLC 写入规则记录失败日志，这里只负责给 IO 返回 NG。
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

        IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups = setup.InstrumentSetups ?? [];
        bool zEnabled = IsSetupEnabled(setup, "ZEnable");
        bool qEnabled = IsSetupEnabled(setup, "QEnable");
        // The primary Ls/Rs set is mandatory for Machine_4_HAHH; LEnable2 only controls an optional second frequency set.
        bool lEnabled = true;

        await SetPolarityStationsEnabledAsync(zEnabled, cancellationToken).ConfigureAwait(false);
        SetPolarityCheckEnabled(zEnabled);
        await ApplyPolarityInstrumentSetupAsync(polMeter1, FindInstrumentSetup(instrumentSetups, "Z1"), FindInstrumentSetup(instrumentSetups, "PHASE1"), zEnabled, cancellationToken).ConfigureAwait(false);
        await ApplyPolarityInstrumentSetupAsync(polMeter2, FindInstrumentSetup(instrumentSetups, "Z2"), FindInstrumentSetup(instrumentSetups, "PHASE2"), zEnabled, cancellationToken).ConfigureAwait(false);
        await ApplyAdexDcrSetupAsync(dcrMeter1, FindInstrumentSetup(instrumentSetups, "DCR1"), cancellationToken).ConfigureAwait(false);
        await ApplyHiokiInductanceSetupAsync(
            indMeter1,
            FindInstrumentSetup(instrumentSetups, "Ls"),
            FindInstrumentSetup(instrumentSetups, "Rs"),
            FindInstrumentSetup(instrumentSetups, "Q"),
            setup,
            lEnabled,
            qEnabled,
            cancellationToken).ConfigureAwait(false);

        // Keep runtime Ls/Rs limits synchronized with the HIOKI 3570 work-order configuration.
        RefreshStationLimitsFromInstrumentConfigs();

        currentTapeSetup = setup.TapeSetup;
        await WriteTapeSetupToPlcAsync(currentTapeSetup, cancellationToken).ConfigureAwait(false);

        currentTapeSetup = setup.TapeSetup;
        await WriteTapeSetupToPlcAsync(currentTapeSetup, cancellationToken).ConfigureAwait(false);

        ApplyStationLimits(instrumentSetups, zEnabled, lEnabled, qEnabled);
    }

    private void ApplyStationLimits(IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups, bool zEnabled, bool lEnabled, bool qEnabled)
    {
        MesWorkOrderInstrumentSetup? dcrSetup = FindInstrumentSetup(instrumentSetups, "DCR1");
        SetStationTestLimit("DCR1", dcrSetup?.LowerLimit, dcrSetup?.UpperLimit, dcrSetup?.Unit);

        if (zEnabled)
        {
            MesWorkOrderInstrumentSetup? zSetup = FindInstrumentSetup(instrumentSetups, "Z1");
            SetStationTestLimit("Z", zSetup?.LowerLimit, zSetup?.UpperLimit, zSetup?.Unit ?? "mΩ");
            SetStationTestLimit("PHASE", FindInstrumentSetup(instrumentSetups, "PHASE1")?.LowerLimit, FindInstrumentSetup(instrumentSetups, "PHASE1")?.UpperLimit, "°");
        }

        if (lEnabled)
        {
            MesWorkOrderInstrumentSetup? lsSetup = FindInstrumentSetup(instrumentSetups, "Ls");
            MesWorkOrderInstrumentSetup? rsSetup = FindInstrumentSetup(instrumentSetups, "Rs");
            SetStationTestLimit("Ls", lsSetup?.LowerLimit, lsSetup?.UpperLimit, lsSetup?.Unit);
            SetStationTestLimit("Rs", rsSetup?.LowerLimit, rsSetup?.UpperLimit, rsSetup?.Unit);
        }

        if (qEnabled)
        {
            MesWorkOrderInstrumentSetup? qSetup = FindInstrumentSetup(instrumentSetups, "Q");
            SetStationTestLimit("Q", qSetup?.LowerLimit, qSetup?.UpperLimit, qSetup?.Unit);
        }
    }

    private void SetPolarityCheckEnabled(bool isEnabled)
    {
        SetCheckOperationEnabled(1, isEnabled);
        SetCheckOperationEnabled(2, isEnabled);
    }

    private async Task SetPolarityStationsEnabledAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        foreach (TestStationModel station in TestStations.Where(static station => station.StationId is 1 or 2))
        {
            await SetStationEnabledAsync(station, isEnabled, cancellationToken).ConfigureAwait(false);
        }
    }

    private void SetCheckOperationEnabled(int stationId, bool isEnabled)
    {
        TestStationModel? station = TestStations.FirstOrDefault(item => item.StationId == stationId);
        if (station == null)
        {
            return;
        }

        StationOperationDescriptor? operation = station.Operations.FirstOrDefault(item => string.Equals(item.Code, StationOperationDescriptor.Check, StringComparison.OrdinalIgnoreCase));
        if (isEnabled)
        {
            if (operation == null)
            {
                station.Operations.Add(new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" });
            }

            return;
        }

        if (operation != null)
        {
            station.Operations.Remove(operation);
        }
    }

    private static MesWorkOrderInstrumentSetup? FindInstrumentSetup(IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups, string parameterId)
        => instrumentSetups.FirstOrDefault(item => string.Equals(item.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase));

    private static bool HasPrimaryInductanceSetup(IReadOnlyList<MesWorkOrderInstrumentSetup> instrumentSetups)
        => instrumentSetups.Any(item =>
            (string.Equals(item.ParameterId, "Ls", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ParameterId, "Rs", StringComparison.OrdinalIgnoreCase))
            && (item.LowerLimit.HasValue || item.UpperLimit.HasValue));

    private static bool IsSetupEnabled(MesWorkOrderSetup setup, string key)
        => setup.Parameters.TryGetString(key, out string value)
            && value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

    private static async Task ApplyPolarityInstrumentSetupAsync(
        IMeasurementInstrument? instrument,
        MesWorkOrderInstrumentSetup? zSetup,
        MesWorkOrderInstrumentSetup? phaseSetup,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        if (!isEnabled || instrument is not IConfigurableDevice configurable || configurable.DeviceParameter is not HiokiLcrConfig config)
        {
            return;
        }

        config.LoadType = HiokiLcrLoadTypes.ZTheta;
        ApplyHiokiPrimaryLimit(config, zSetup, "Ω");
        ApplyHiokiSecondaryLimit(config, phaseSetup, "°");
        if (!string.IsNullOrWhiteSpace(zSetup?.Range))
        {
            config.Range = NormalizeHiokiRange(zSetup.Range);
        }

        await configurable.ApplyConfigAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyHiokiInductanceSetupAsync(
        IMeasurementInstrument? instrument,
        MesWorkOrderInstrumentSetup? lsSetup,
        MesWorkOrderInstrumentSetup? rsSetup,
        MesWorkOrderInstrumentSetup? qSetup,
        MesWorkOrderSetup setup,
        bool lEnabled,
        bool qEnabled,
        CancellationToken cancellationToken)
    {
        if (!lEnabled)
        {
            return;
        }

        if (instrument == null)
        {
            throw new InvalidOperationException("Primary Ls/Rs setup exists, but HIOKI Ind.01 is not bound.");
        }

        if (instrument is not IConfigurableDevice configurable || configurable.DeviceParameter is not HiokiLcrConfig config)
        {
            throw new InvalidOperationException($"Primary Ls/Rs setup cannot be applied: {instrument.DeviceName} is not a configurable HIOKI LCR instrument.");
        }

        if (lsSetup == null)
        {
            throw new InvalidOperationException("Primary Ls setup is missing from the parsed work-order data.");
        }

        if (!qEnabled && rsSetup == null)
        {
            throw new InvalidOperationException("Primary Rs setup is missing from the parsed work-order data.");
        }

        if (qEnabled && qSetup == null)
        {
            throw new InvalidOperationException("Q is enabled, but its setup is missing from the parsed work-order data.");
        }

        config.LoadType = qEnabled ? HiokiLcrLoadTypes.LsQ : HiokiLcrLoadTypes.LsRs;
        ApplyHiokiPrimaryLimit(config, lsSetup, lsSetup?.Unit ?? "μH");
        ApplyHiokiSecondaryLimit(config, qEnabled ? qSetup : rsSetup, qEnabled ? string.Empty : rsSetup?.Unit ?? "mΩ");
        if (setup.Parameters.TryGetDouble("LFreq", out double frequency) && frequency > 0)
        {
            // Cyntec work-order LFreq is expressed in kHz (legacy MES contract).
            // HIOKI parameter commands use the base frequency unit (Hz).
            config.Frequency = frequency * 1_000d;
            config.FrequencyUnit = "Hz";
        }

        if (!string.IsNullOrWhiteSpace(lsSetup?.Range))
        {
            config.Range = NormalizeHiokiRange(lsSetup.Range);
        }

        await configurable.ApplyConfigAsync(cancellationToken).ConfigureAwait(false);
    }

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

    private static void ApplyHiokiPrimaryLimit(HiokiLcrConfig config, MesWorkOrderInstrumentSetup? setup, string defaultUnit)
    {
        if (setup == null)
        {
            return;
        }

        // HIOKI keeps a unit alongside each comparator edge.  MES supplies one
        // unit per measurement, so keep both edges synchronized even if an
        // individual limit value is absent in the source payload.
        string unit = string.IsNullOrWhiteSpace(setup.Unit) ? defaultUnit : setup.Unit;
        config.Parameter1MinUnit = unit;
        config.Parameter1MaxUnit = unit;

        if (setup?.LowerLimit.HasValue == true)
        {
            config.Parameter1Min = setup.LowerLimit.Value;
        }

        if (setup?.UpperLimit.HasValue == true)
        {
            config.Parameter1Max = setup.UpperLimit.Value;
        }
    }

    private static void ApplyHiokiSecondaryLimit(HiokiLcrConfig config, MesWorkOrderInstrumentSetup? setup, string defaultUnit)
    {
        if (setup == null)
        {
            return;
        }

        // See ApplyHiokiPrimaryLimit: Parameter3MaxUnit must not rely on the
        // presence of Parameter3Max, otherwise the Rs upper limit can display
        // or be sent without its mΩ unit after a work-order refresh.
        string unit = string.IsNullOrWhiteSpace(setup.Unit) ? defaultUnit : setup.Unit;
        config.Parameter3MinUnit = unit;
        config.Parameter3MaxUnit = unit;

        if (setup?.LowerLimit.HasValue == true)
        {
            config.Parameter3Min = setup.LowerLimit.Value;
        }

        if (setup?.UpperLimit.HasValue == true)
        {
            config.Parameter3Max = setup.UpperLimit.Value;
        }
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
        return normalized.Equals("uΩ", StringComparison.OrdinalIgnoreCase)
            ? "μΩ"
            : normalized;
    }

    private static string NormalizeHiokiRange(string range)
    {
        string normalized = range.Trim();
        return normalized.Replace("kΩ", "KΩ", StringComparison.Ordinal);
    }
    public async Task ApplyBraidSetupAsync(MesWorkOrderTapeSetup tapeSetup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tapeSetup);
        currentTapeSetup = tapeSetup;
        await WriteTapeSetupToPlcAsync(currentTapeSetup, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyMarkPrintStringAsync(string? printString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printString))
        {
            return;
        }

        if (markPrintDevice is not { IsConnected: true })
        {
            throw new InvalidOperationException("Mark print device is not connected.");
        }

        await markPrintDevice.SetPrintStringAsync(printString, cancellationToken).ConfigureAwait(false);
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
        await WriteUInt32PointAsync(plc, PlcPoints.样品后空格, tapeSetup.BlankQty, cancellationToken).ConfigureAwait(false);
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

        string[] lines =
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
            BuildSummaryLine("CpkDCR1", FormatNullableCpk(CalculateCpk("DCR1", dcr1Cpk))),
            BuildSummaryLine("CpkLS", FormatNullableCpk(CalculateCpk("Ls", lsCpk))),
            BuildSummaryLine("CpkRS", FormatNullableCpk(CalculateCpk("Rs", rsCpk)))
        ];

        await TextFileHelper.WriteAsync(directory, fileName, string.Join(Environment.NewLine, lines), appendNewLine: false).ConfigureAwait(false);
    }

    protected override Task ProcessTestRecordAsync(TestResultPayload record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (record.Type != RecordType.Numeric)
        {
            return Task.CompletedTask;
        }

        Machine4HahhTestOutputRecord? completedRecord = null;
        lock (pendingTestRecords)
        {
            if (string.Equals(record.Name, "DCR1", StringComparison.OrdinalIgnoreCase))
            {
                dcr1Cpk.Add(record.TestValue);
                Machine4HahhTestOutputRecord outputRecord = new() { Dcr1 = record.TestValue, Dcr1Result = record.Judge, Dcr1TestTime = DateTime.Now };
                if (record.Judge == false)
                {
                    completedRecord = outputRecord;
                }
                else
                {
                    pendingTestRecords.Enqueue(outputRecord);
                }
            }
            else if (string.Equals(record.Name, "Ls", StringComparison.OrdinalIgnoreCase))
            {
                lsCpk.Add(record.TestValue);
                Machine4HahhTestOutputRecord outputRecord = pendingTestRecords.Count > 0 ? pendingTestRecords.Dequeue() : new Machine4HahhTestOutputRecord();
                outputRecord.Ls = record.TestValue;
                outputRecord.LsResult = record.Judge;
                outputRecord.LsTestTime = DateTime.Now;
                if (record.Judge == false)
                {
                    completedRecord = outputRecord;
                }
                else
                {
                    pendingTestRecords.Enqueue(outputRecord);
                }
            }
            else if (string.Equals(record.Name, "Rs", StringComparison.OrdinalIgnoreCase))
            {
                rsCpk.Add(record.TestValue);
                Machine4HahhTestOutputRecord outputRecord = pendingTestRecords.Count > 0 ? pendingTestRecords.Dequeue() : new Machine4HahhTestOutputRecord();
                outputRecord.Rs = record.TestValue;
                outputRecord.RsResult = record.Judge;
                outputRecord.RsTestTime = DateTime.Now;
                completedRecord = outputRecord;
            }
        }

        if (completedRecord.HasValue)
        {
            EnqueueTestOutputRecord(completedRecord.Value);
        }

        return Task.CompletedTask;
    }

    private void EnqueueTestOutputRecord(Machine4HahhTestOutputRecord record)
    {
        IProductionRecordWriter writer = productionRecordWriter ?? throw new InvalidOperationException("Production record writer is not configured.");
        string directory = ProductionRecordPathHelper.RuntimeDirectory;
        string fileName = ProductionRecordPathHelper.BuildFileName(productionContext?.WorkOrderNo, MachineId);
        string[] fields =
        [
            FormatNullableValue(record.Dcr1),
            FormatNullableJudge(record.Dcr1Result),
            FormatNullableTime(record.Dcr1TestTime),
            FormatNullableValue(record.Ls),
            FormatNullableJudge(record.LsResult),
            FormatNullableTime(record.LsTestTime),
            FormatNullableValue(record.Rs),
            FormatNullableJudge(record.RsResult),
            FormatNullableTime(record.RsTestTime),
            EscapeCsv(productionContext?.OperatorNo)
        ];

        if (!writer.TryEnqueue(new ProductionRecordWriteRequest(directory, fileName, fields)))
        {
            throw new InvalidOperationException($"Failed to enqueue production record: {Path.Combine(directory, fileName)}");
        }
    }

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

    private void ResetRunSummaryState()
    {
        dcr1Cpk.Reset();
        lsCpk.Reset();
        rsCpk.Reset();
        pendingTestRecords.Clear();
    }
    public override Task<MachineExamineResult> ExecuteExamineAsync(string flowCode, IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!ExamineFlows.TryGetValue(flowCode, out MachineExamineFlowDescriptor? flow))
        {
            return base.ExecuteExamineAsync(flowCode, progress, cancellationToken);
        }

        return IsPolarityFlow(flow.Code)
            ? ExecutePolarityExamineFlowAsync(flow, progress, cancellationToken)
            : ExecuteExamineFlowAsync(flow, progress, cancellationToken);
    }

    private async Task<MachineExamineResult> ExecutePolarityExamineFlowAsync(
        MachineExamineFlowDescriptor flow,
        IProgress<MachineExamineMeasurement>? progress,
        CancellationToken cancellationToken)
    {
        IPlcDevice? plc = Plc;
        var measurements = new List<MachineExamineMeasurement>();
        if (plc == null || !plc.IsConnected)
        {
            return MachineExamineResult.Failed("PLC is not connected.", measurements);
        }

        await plc.WriteInt16Async(PlcAddressCache[flow.SamplePointKey], 1, cancellationToken).ConfigureAwait(false);
        await plc.WriteInt16Async(PlcAddressCache[flow.StartPointKey], 1, cancellationToken).ConfigureAwait(false);

        bool requirePositive = IsPolarityForwardFlow(flow.Code);
        bool allPassed = true;
        string? failureMessage = null;
        int repeatCount = Math.Max(1, flow.RepeatCount);
        foreach (MachineExamineStepDescriptor step in flow.Steps)
        {
            bool ready = await WaitPlcSignalAsync(plc, PlcAddressCache[step.TriggerPointKey], (ushort)1, timeoutMs: step.TimeoutMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!ready)
            {
                return MachineExamineResult.Failed(measurements: measurements);
            }

            var stationPhaseValues = new List<double>(repeatCount);
            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                MachineExamineMeasurement? measurement = await ReadStationMeasurementAsync(step.StationId, step.TestName, cancellationToken).ConfigureAwait(false);
                if (measurement == null)
                {
                    return MachineExamineResult.Failed(measurements: measurements);
                }

                measurements.Add(measurement);
                progress?.Report(measurement);
                if (!TryGetMeasurementValue(measurement, "PHASE", out double phaseValue))
                {
                    allPassed = false;
                    failureMessage ??= $"{flow.Code} polarity check failed. {measurement.StationName} returned no PHASE value.";
                }
                else
                {
                    stationPhaseValues.Add(phaseValue);
                }

                if (repeatIndex < repeatCount - 1)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }

            bool stationPassed = stationPhaseValues.Count == repeatCount
                && stationPhaseValues.All(value => IsPolarityPhaseValuePassed(value, requirePositive));
            if (!stationPassed)
            {
                allPassed = false;
                string directionText = requirePositive ? "greater than 0" : "less than 0";
                failureMessage ??= $"{flow.Code} polarity check failed. Station {step.StationId} PHASE values must all be {directionText}.";
            }

            await plc.WriteInt16Async(PlcAddressCache[step.ReadCompletedPointKey], 1, cancellationToken).ConfigureAwait(false);
        }

        await plc.WriteInt16Async(PlcAddressCache[flow.CompletedPointKey], 1, cancellationToken).ConfigureAwait(false);
        return allPassed
            ? MachineExamineResult.Completed(measurements)
            : MachineExamineResult.Failed(failureMessage, measurements);
    }

    private static bool IsPolarityFlow(string flowCode)
        => IsPolarityForwardFlow(flowCode) || IsPolarityReverseFlow(flowCode);

    private static bool IsPolarityForwardFlow(string flowCode)
        => string.Equals(flowCode, "PolarityForward", StringComparison.OrdinalIgnoreCase);

    private static bool IsPolarityReverseFlow(string flowCode)
        => string.Equals(flowCode, "PolarityReverse", StringComparison.OrdinalIgnoreCase);

    private static bool IsPolarityPhaseValuePassed(double phaseValue, bool requirePositive)
        => !double.IsNaN(phaseValue)
            && !double.IsInfinity(phaseValue)
            && (requirePositive ? phaseValue > 0 : phaseValue < 0);

    private static bool TryGetMeasurementValue(MachineExamineMeasurement measurement, string valueName, out double value)
    {
        InstrumentMeasurementValue? measurementValue = measurement.Measurement.Values.FirstOrDefault(item => string.Equals(item.Name, valueName, StringComparison.OrdinalIgnoreCase));
        if (measurementValue == null && measurement.Measurement.Values.Count > 0)
        {
            measurementValue = measurement.Measurement.Values[0];
        }

        value = measurementValue?.Value ?? 0;
        return measurementValue != null;
    }

    public override async Task SetCheckViewActiveAsync(bool isActive, CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.进入点检界面, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, isActive ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SetCheckCompletedAsync(bool isCompleted, CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.点检完成, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, isCompleted ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }

    public override Task OnCompensateScheduleExpiredAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
        => SetCheckCompletedAsync(false, cancellationToken);

    public async Task<bool> ResetProductionCounterAsync(CancellationToken cancellationToken = default)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true }
            || !PlcAddressCache.TryGetValue((int)PlcPoints.统计计数清零, out string? address))
        {
            return false;
        }

        await plc.WriteInt16Async(address, 1, cancellationToken).ConfigureAwait(false);
        return true;
    }
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

    private bool ShouldWriteForceBraidNewWorkOrderSignal()
        => !string.IsNullOrWhiteSpace(currentWorkOrderNo)
            && !string.Equals(forceBraidSignalWrittenWorkOrderNo, currentWorkOrderNo, StringComparison.OrdinalIgnoreCase);

    private async Task WriteForceBraidNewWorkOrderSignalAsync(bool value, CancellationToken cancellationToken)
    {
        IPlcDevice? plc = Plc;
        if (plc is not { IsConnected: true } || !PlcAddressCache.TryGetValue((int)PlcPoints.强制编带_新工单, out string? address))
        {
            return;
        }

        await plc.WriteInt16Async(address, value ? (short)1 : (short)0, cancellationToken).ConfigureAwait(false);
    }

    private async Task FlushProductionRecordsAsync()
    {
        if (productionRecordWriter == null)
        {
            return;
        }

        await productionRecordWriter.FlushAsync().ConfigureAwait(false);
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
        if (await IsPlcStopSignalActiveAsync(PlcPoints.编带电机释放, cancellationToken).ConfigureAwait(false))
        {
            signals.Add(new MachinePlcStopSignal(MachinePlcStopSignalKind.TapeMotorRelease, ClearTablePaperCode: true, ResetAfterHandled: true));
        }

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
            MachinePlcStopSignalKind.TapeMotorRelease => PlcPoints.编带电机释放,
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
}

internal sealed record Machine4HahhStationStatisticsSnapshot(uint Total, uint Ng, uint Ce);

internal struct Machine4HahhTestOutputRecord
{
    public double? Dcr1 { get; set; }
    public bool? Dcr1Result { get; set; }
    public DateTime? Dcr1TestTime { get; set; }
    public double? Ls { get; set; }
    public bool? LsResult { get; set; }
    public DateTime? LsTestTime { get; set; }
    public double? Rs { get; set; }
    public bool? RsResult { get; set; }
    public DateTime? RsTestTime { get; set; }
}



