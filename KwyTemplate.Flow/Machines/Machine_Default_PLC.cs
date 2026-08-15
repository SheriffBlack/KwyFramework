using System.Collections.Concurrent;
using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// Demo PLC 机台示例：集中定义本机型的 IO、PLC 点位、工站和专属业务逻辑。
/// 新增机型时可参考该类，不需要改 MachineBase。
/// </summary>
public sealed class Machine_Default_PLC : MachineBase
{
    private IMeasurementInstrument? dcrMeter1;
    public Machine_Default_PLC(IMachineDeviceContext devices)
        : base(devices)
    {
        BindDevices();
        InitTestStations();
        BuildDataGrid();
    }

    public bool IsGreenLightOn { get; private set; }

    public bool IsAlarmOn { get; private set; }

    public bool IsDangerOutputEnabled { get; private set; }

    public override TriggerMode StationTriggerMode => TriggerMode.Polling;

    /// <summary>
    /// PC 写给 IO 卡的点位。
    /// </summary>
    public enum PcToCard
    {
        ParameterCompare = 0,
        PolarityFinished = 1,
        ResistanceFinished = 2,
        InductanceFinished = 3,
        ReservedFinished = 4,
        FrontCameraFinished = 5,
        BackCameraFinished = 6,
        BarcodeTrigger = 7,
        PolarityOk = 8,
        ResistanceOk = 9,
        InductanceOk = 10,
        ReservedOk = 11,
        FrontCameraOk = 12,
        BackCameraOk = 13,
        BarcodeOk = 14,
        BarcodeNg = 15,
    }

    /// <summary>
    /// PC 从 IO 卡读取的点位。
    /// </summary>
    public enum CardToPc
    {
        PcOnline = 0,
        PolarityReceivedFinished = 1,
        ResistanceReceivedFinished = 2,
        InductanceReceivedFinished = 3,
        ReservedReceivedFinished = 4,
        FrontCameraFinished = 5,
        BackCameraFinished = 6,
        BarcodeStation = 7,
        ParameterCompareOk = 8,
        ParameterCompareNg = 9,
        MesClearMaterial = 10,
        LsOk = 11,
        RsOk = 12
    }

    public enum PlcPoints
    {
        [PlcPoint("M100", typeof(bool))]
        PcOnlineHeartbeat,

        [PlcPoint("M101", typeof(bool))]
        PolarityTestOver,

        [PlcPoint("R100", typeof(bool))]
        BadProductBoxLock1Manual,

        [PlcPoint("R101", typeof(bool))]
        BadProductBoxLock2Manual,

        [PlcPoint("R102", typeof(bool))]
        BadProductBoxLock3Manual,

        [PlcPoint("R103", typeof(bool))]
        BadProductBoxLock4Manual,

        [PlcPoint("R104", typeof(bool))]
        BadProductBoxLock5Manual,

        [PlcPoint("R130", typeof(bool), IsReadOnly = true)]
        WearingPartCountReachedAlarm,

        [PlcPoint("R131", typeof(bool), IsReadOnly = true)]
        AirPressureDetectionAlarm,

        [PlcPoint("DM100", typeof(int))]
        CurrentQuantity,

        [PlcPoint("DM102", typeof(int))]
        SetQuantity
    }

    /// <summary>
    /// 不良品盒手动开关。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<MachinePlcPointDefinition> GetCassetteSwitchPoints()
        => GetPlcPoints(
            PlcPoints.BadProductBoxLock1Manual,
            PlcPoints.BadProductBoxLock2Manual,
            PlcPoints.BadProductBoxLock3Manual,
            PlcPoints.BadProductBoxLock4Manual,
            PlcPoints.BadProductBoxLock5Manual);

    /// <summary>
    /// 报警监控点位。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<MachinePlcPointDefinition> GetAlarmMonitorPoints()
        => GetPlcPoints(
            PlcPoints.WearingPartCountReachedAlarm,
            PlcPoints.AirPressureDetectionAlarm);

    /// <summary>
    /// 寄存器监控点位。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<MachinePlcPointDefinition> GetRegisterMonitorPoints()
        => GetPlcPoints(
            PlcPoints.CurrentQuantity,
            PlcPoints.SetQuantity);
    public override void BindDevices()
    {
        RegisterPlcPoints();

        if (Devices.TryGet<IPlcDevice>(DeviceIds.MainPlc, out IPlcDevice? mainPlc))
        {
            BindPlc(mainPlc);
        }

        if (Devices.TryGet<Kwy.Device.Abstractions.IO.IIoCardDevice>(DeviceIds.MainIoCard, out Kwy.Device.Abstractions.IO.IIoCardDevice? mainIoCard) && mainIoCard != null)
        {
            base.BindIoCard(mainIoCard);
            RegisterCardToPcNames(mainIoCard);
            RegisterPcToCardNames(mainIoCard);
        }

        if (Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Dcr", 1), out IMeasurementInstrument? dcr))
        {
            dcrMeter1 = dcr;
        }
    }


    private static void RegisterCardToPcNames(Kwy.Device.Abstractions.IO.IIoCardDevice card)
    {
        foreach (CardToPc input in Enum.GetValues<CardToPc>())
        {
            card.SetDiName((int)input, GetDescription(input));
        }
    }

    private static void RegisterPcToCardNames(Kwy.Device.Abstractions.IO.IIoCardDevice card)
    {
        foreach (PcToCard output in Enum.GetValues<PcToCard>())
        {
            card.SetDoName((int)output, GetDescription(output));
        }
    }

    private void RegisterPlcPoints()
    {
        RegisterAndCachePlcPoints<PlcPoints>();
    }


    public override void InitTestStations()
    {
        TestStations =
        [
            new TestStationModel
            {
                StationId = 1,
                StationName = "Polarity",
                OrderedTestNames = ["Polarity"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Polarity"] = 0
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Polarity"] = true
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = (int)CardToPc.PolarityReceivedFinished,
                    ResultOkInput = (int)CardToPc.ParameterCompareOk,
                    ResultReadCompletedOutput = (int)PcToCard.PolarityFinished
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" }
                },
                StationDataDeals = [new StationIoResultDataDeal("Polarity")]
            },
            new TestStationModel
            {
                StationId = 2,
                StationName = "电阻",
                OrderedTestNames = ["DCR"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DCR"] = 0
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DCR"] = true
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = (int)CardToPc.ResistanceReceivedFinished,
                    ResultOkInput = (int)CardToPc.RsOk,
                    ResultReadCompletedOutput = (int)PcToCard.ResistanceFinished
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" }
                },
                StationDataDeals = [new InstrumentMeasurementDataDeal("DCR", dcrMeter1)]
            },
            new TestStationModel
            {
                StationId = 3,
                StationName = "综合",
                OrderedTestNames = ["Ls", "Rs"],
                TestValues = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Ls"] = 0,
                    ["Rs"] = 0
                },
                TestJudges = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Ls"] = true,
                    ["Rs"] = true
                },
                StationIo = new StationIoBinding
                {
                    TestFinishedInput = (int)CardToPc.InductanceReceivedFinished,
                    ResultOkInput = (int)CardToPc.LsOk,
                    ResultReadCompletedOutput = (int)PcToCard.InductanceFinished
                },
                Operations =
                {
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Check, DisplayName = "点检" },
                    new StationOperationDescriptor { Code = StationOperationDescriptor.Calibration, DisplayName = "校正" }
                },
                StationDataDeals = [new DemoLsRsDataDeal()],
                ParallelDeals = false
            }
        ];
    }

    protected override void ReadSystemData()
    {
        if (Plc == null || !Plc.IsConnected)
        {
            return;
        }

        string heartbeatAddress = PlcAddressCache[(int)PlcPoints.PcOnlineHeartbeat];
        _ = Plc.WriteBoolAsync(heartbeatAddress, true);
    }

    /// <summary>
    /// 结果消费/保存链路。
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task ProcessTestRecordAsync(TestResultPayload record, CancellationToken cancellationToken)
    {
        if (record.Type == RecordType.Numeric)
        {
            await Task.Yield();
            System.Diagnostics.Debug.WriteLine($"[DemoPLC] Numeric value: {record.TestValue:F4} can be sent to MES or saved locally.");
            return;
        }

        await Task.Yield();
        System.Diagnostics.Debug.WriteLine($"[DemoPLC] Numeric value: {record.TestValue:F4} can be sent to MES or saved locally.");
    }

    /// <summary>
    /// 点检/标准件检查：等待某个测试完成信号变为 true，实际设备可扩展为 PLC 到位、触发仪表、读取标准件并判断是否合格。
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task<MachineExamineResult> ExecuteExamineStandardAsync(IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
    {
        bool isOver = await WaitPlcSignalAsync(Plc, PlcAddressCache[(int)PlcPoints.PolarityTestOver], timeoutMs: 5000, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!isOver)
        {
            return MachineExamineResult.Failed();
        }

        bool isReady = await WaitPlcSignalAsync(Plc, PlcAddressCache[(int)PlcPoints.PolarityTestOver], timeoutMs: 3000, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return isReady ? MachineExamineResult.Completed([]) : MachineExamineResult.Failed();
    }

    /// <summary>
    /// 刷新寄存器数据
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task RefreshRegisterSnapshotAsync(CancellationToken cancellationToken = default)
    {
    /// 刷新寄存器快照。
        {
            return;
        }

        foreach (MachinePlcPointDefinition point in GetRegisterMonitorPoints())
        {
            if (point.DataType == typeof(int))
            {
                _ = await Plc.ReadInt32ArrayAsync(point.Address, 1, cancellationToken).ConfigureAwait(false);
            }
        }
    }


    /// <summary>
    /// 测试启动时复位报警并打开运行状态。
    /// </summary>
    protected override Task OnTestStartedAsync(CancellationToken cancellationToken)
    {
        IsAlarmOn = false;
        IsGreenLightOn = true;
        IsDangerOutputEnabled = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试停止时切断危险输出并进入报警状态。
    /// </summary>
    protected override Task OnTestStoppedAsync(CancellationToken cancellationToken)
    {
        IsDangerOutputEnabled = false;
        IsGreenLightOn = false;
        IsAlarmOn = true;
        return Task.CompletedTask;
    }



    private sealed class DemoLsRsDataDeal : IStationDataDeal
    {
        public Task<IStationDataCapture> CaptureAsync(CancellationToken cancellationToken = default)
        {
            double ls = Random.Shared.NextDouble() * 0.1 + 1.0;
            double rs = Random.Shared.NextDouble() * 0.02 + 0.1;
            return Task.FromResult<IStationDataCapture>(new DemoLsRsCapture(ls, rs));
        }

        public void ApplyCapture(IStationDataCapture capture, bool triggerResult, TestStationModel station)
        {
            if (capture is not DemoLsRsCapture values)
            {
                throw new ArgumentException("Measurement capture type does not match the data deal.", nameof(capture));
            }

            station.TestValues["Ls"] = values.Ls;
            station.TestValues["Rs"] = values.Rs;
            station.TestJudges["Ls"] = triggerResult && values.Ls is >= 1.0 and <= 1.1;
            station.TestJudges["Rs"] = triggerResult && values.Rs is >= 0.1 and <= 0.12;
        }

        private sealed record DemoLsRsCapture(double Ls, double Rs) : IStationDataCapture;
    }
}




































