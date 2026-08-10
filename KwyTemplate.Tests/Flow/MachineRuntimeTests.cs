using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using KwyTemplate.App.Runtime;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.Flow.Services;
using KwyTemplate.MES.Abstract.Models;
using Xunit;

namespace KwyTemplate.Tests.Flow;

public sealed class MachineRuntimeTests
{
    [Fact]
    public async Task PauseAsync_DoesNotChangeRunningStateOrRaiseRunningStateChanged()
    {
        var machine = new TestMachine(new FakeMachineDeviceContext());
        int runningStateChangedCount = 0;
        machine.RunningStateChanged += (_, _) => runningStateChangedCount++;

        await machine.StartAsync();
        Assert.True(machine.IsRunning);
        Assert.Equal(1, machine.StartedCount);
        Assert.Equal(1, runningStateChangedCount);

        await machine.PauseAsync();
        Assert.True(machine.IsRunning);
        Assert.Equal(1, machine.PausedCount);
        Assert.Equal(1, runningStateChangedCount);

        await machine.StopAsync();
        Assert.False(machine.IsRunning);
        Assert.Equal(1, machine.StoppedCount);
        Assert.Equal(2, runningStateChangedCount);

        await machine.StopRuntimeAsync();
    }

    [Fact]
    public async Task SetStationEnabledAsync_UsesExplicitStationSwitchPointKeyForExternalStations()
    {
        var plc = new FakePlcDevice();
        var machine = CreateMachine2A(plc);
        TestStationModel station3 = Assert.Single(machine.TestStations, item => item.StationId == 3);
        TestStationModel station4 = Assert.Single(machine.TestStations, item => item.StationId == 4);

        await machine.SetStationEnabledAsync(station3, true);
        await machine.SetStationEnabledAsync(station4, false);

        Assert.Equal((short)1, plc.Int16Writes["DM6110"]);
        Assert.Equal((short)0, plc.Int16Writes["DM6114"]);
        Assert.Equal((short)1, plc.Int16Writes["DM6130"]);
        Assert.True(station3.IsEnabled);
        Assert.False(station4.IsEnabled);
    }

    [Fact]
    public async Task ReadStationEnabledAsync_ReadsExplicitStationSwitchPointKeyFromPlc()
    {
        var plc = new FakePlcDevice();
        plc.Int16Reads["DM6110"] = 1;
        var machine = CreateMachine2A(plc);
        TestStationModel station3 = Assert.Single(machine.TestStations, item => item.StationId == 3);

        bool? enabled = await machine.ReadStationEnabledAsync(station3);

        Assert.True(enabled);
    }

    [Fact]
    public async Task ProcessTestRecord_BuffersDcr1OkUntilDcr2CompletesSamePart()
    {
        var writer = new RecordingProductionRecordWriter();
        var context = new ProductionContext { WorkOrderNo = "WO001", OperatorNo = "OP01" };
        var machine = CreateMachine2A(new FakePlcDevice(), context, writer: writer);

        await machine.ProcessRecordAsync(new TestResultPayload(RecordType.Numeric, "DCR1", 12.34567, true));
        Assert.Empty(writer.Requests);

        await machine.ProcessRecordAsync(new TestResultPayload(RecordType.Numeric, "DCR2", 23.45678, false));

        ProductionRecordWriteRequest request = Assert.Single(writer.Requests);
        Assert.Equal("WO001.txt", request.FileName);
        Assert.Equal("12.3457", request.FieldsWithoutSequence[0]);
        Assert.Equal("OK", request.FieldsWithoutSequence[1]);
        Assert.Equal("23.4568", request.FieldsWithoutSequence[2]);
        Assert.Equal("NG", request.FieldsWithoutSequence[3]);
        Assert.Equal("OP01", request.FieldsWithoutSequence[4]);
    }

    [Fact]
    public async Task ProcessTestRecord_Dcr1NgCompletesImmediatelyAndFillsDcr2WithNull()
    {
        var writer = new RecordingProductionRecordWriter();
        var context = new ProductionContext { WorkOrderNo = "WO001", OperatorNo = "OP01" };
        var machine = CreateMachine2A(new FakePlcDevice(), context, writer: writer);

        await machine.ProcessRecordAsync(new TestResultPayload(RecordType.Numeric, "DCR1", 9.1, false));

        ProductionRecordWriteRequest request = Assert.Single(writer.Requests);
        Assert.Equal("9.1000", request.FieldsWithoutSequence[0]);
        Assert.Equal("NG", request.FieldsWithoutSequence[1]);
        Assert.Equal("(NULL)", request.FieldsWithoutSequence[2]);
        Assert.Equal("(NULL)", request.FieldsWithoutSequence[3]);
    }

    [Fact]
    public async Task SaveProductionSummaryAsync_WritesConfiguredSummaryFieldsAndOverwritesFile()
    {
        string directory = CreateTempDirectory();
        var plc = new FakePlcDevice();
        plc.Int32Reads["DM6502"] = 2000;
        plc.FloatReads["DM6500"] = 0.9f;
        plc.Int32Reads["DM6506"] = 200;
        plc.Int16Reads["E9986"] = 20;
        var context = new ProductionContext { WorkOrderNo = "WO-SUM" };
        var options = new TestProductionOutputOptions(directory);
        var machine = CreateMachine2A(plc, context, options);
        await machine.ApplyBraidSetupAsync(new MesWorkOrderTapeSetup(null, null, null, 10, null, null));
        await File.WriteAllTextAsync(Path.Combine(directory, "WO-SUM.txt"), "old-data");

        await machine.SaveProductionSummaryAsync();

        string[] lines = await File.ReadAllLinesAsync(Path.Combine(directory, "WO-SUM.txt"));
        Assert.Equal(11, lines.Length);
        Assert.Equal("OutputQty，OutputQty，数值，颗，2000，自动", lines[0]);
        Assert.Equal("Yeld，Yeld，数值，颗，90%，自动", lines[1]);
        Assert.Equal("NGSum，NGSum，数值，颗，200，自动", lines[2]);
        Assert.Equal("TCAddQty，TCAddQty，数值，颗，20，自动", lines[3]);
        Assert.Equal("LeaveQty，LeaveQty，数值，颗，0，自动", lines[4]);
        Assert.Equal("Remark，Remark，文字，，抽检用料10颗，手动输入", lines[5]);
        Assert.Equal("AdjustQty，AdjustQty，数值，颗，0，自动", lines[6]);
        Assert.Equal("NGPOLAR1，NGPOLAR1，数值，颗，0，自动", lines[7]);
        Assert.Equal("NGPOLAR2，NGPOLAR2，数值，颗，0，自动", lines[8]);
        Assert.DoesNotContain("old-data", string.Join('\n', lines));
    }

    private static Machine2ATestHarness CreateMachine2A(
        FakePlcDevice plc,
        ProductionContext? context = null,
        IProductionOutputOptions? options = null,
        IProductionRecordWriter? writer = null)
    {
        var devices = new FakeMachineDeviceContext();
        devices.Add(DeviceIds.MainPlc, plc);
        return new Machine2ATestHarness(devices, context, options, writer);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "KwyTemplateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestMachine(IMachineDeviceContext devices) : MachineBase(devices)
    {
        public int StartedCount { get; private set; }
        public int PausedCount { get; private set; }
        public int StoppedCount { get; private set; }
        public override TriggerMode StationTriggerMode => TriggerMode.Programmatic;
        public override void InitTestStations() => TestStations = [];
        protected override Task OnTestStartedAsync(CancellationToken cancellationToken) { StartedCount++; return Task.CompletedTask; }
        protected override Task OnTestPausedAsync(CancellationToken cancellationToken) { PausedCount++; return Task.CompletedTask; }
        protected override Task OnTestStoppedAsync(CancellationToken cancellationToken) { StoppedCount++; return Task.CompletedTask; }
    }

    private sealed class Machine2ATestHarness(
        IMachineDeviceContext devices,
        IProductionRuntimeContext? context,
        IProductionOutputOptions? options,
        IProductionRecordWriter? writer)
        : Machine_2_A(devices, context, options, writer)
    {
        public Task ProcessRecordAsync(TestResultPayload record)
            => ProcessTestRecordAsync(record, CancellationToken.None);
    }

    private sealed class TestProductionOutputOptions(string summaryDirectory) : IProductionOutputOptions
    {
        public string OutputDirectory => summaryDirectory;
        public string SummaryDirectory => summaryDirectory;
    }

    private sealed class RecordingProductionRecordWriter : IProductionRecordWriter
    {
        public List<ProductionRecordWriteRequest> Requests { get; } = [];
        public bool TryEnqueue(ProductionRecordWriteRequest request) { Requests.Add(request); return true; }
        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<bool> MoveAsync(string sourceDirectory, string sourceFileName, string targetDirectory, string? targetFileName = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class FakeMachineDeviceContext : IMachineDeviceContext
    {
        private readonly Dictionary<string, object> devices = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<IDevice> Devices => devices.Values.OfType<IDevice>().ToArray();
        public void Add<TDevice>(string deviceId, TDevice device) where TDevice : class => devices[deviceId] = device;
        public bool TryGet<TDevice>(string deviceId, out TDevice? device) where TDevice : class
        {
            if (devices.TryGetValue(deviceId, out object? value) && value is TDevice typed)
            {
                device = typed;
                return true;
            }

            device = null;
            return false;
        }

        public TDevice GetRequired<TDevice>(string deviceId) where TDevice : class
            => TryGet(deviceId, out TDevice? device) && device != null ? device : throw new KeyNotFoundException(deviceId);

        public IReadOnlyCollection<TDevice> GetAll<TDevice>() where TDevice : class
            => devices.Values.OfType<TDevice>().ToArray();
    }

    private sealed class FakePlcDevice : IPlcDevice
    {
        public Dictionary<string, short> Int16Reads { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Int32Reads { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> FloatReads { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, short> Int16Writes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Int32Writes { get; } = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PlcPointInfoModel> points = [];
        public string DeviceId => DeviceIds.MainPlc;
        public string DeviceName => "Fake PLC";
        public bool IsConnected { get; set; } = true;
        public ConnectionState State => IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected;
        public IDeviceConfig DeviceParameter { get; set; } = new FakeDeviceConfig();
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
        public event EventHandler<DeviceOperationEventArgs>? OperationOccurred { add { } remove { } }
        public Task ConnectAsync(CancellationToken cancellationToken = default) { IsConnected = true; return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) { IsConnected = false; return Task.CompletedTask; }
        public Task ApplyConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(ReadInt16(address) != 0);
        public Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default) => Task.FromResult(ReadInt16(address));
        public Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(FloatReads.GetValueOrDefault(address));
        public Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default) => Task.FromResult(new byte[length]);
        public Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Repeat(ReadInt16(address), count).ToArray());
        public Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Repeat(Int32Reads.GetValueOrDefault(address), count).ToArray());
        public Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Repeat(FloatReads.GetValueOrDefault(address), count).ToArray());
        public Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default) { Int16Writes[address] = value ? (short)1 : (short)0; return Task.CompletedTask; }
        public Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default) { Int16Writes[address] = value; return Task.CompletedTask; }
        public Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default) { Int32Writes[address] = value; return Task.CompletedTask; }
        public Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default) { FloatReads[address] = value; return Task.CompletedTask; }
        public Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RegisterPoint(string address, string name, Type dataType, bool isReadOnly = false) => points.Add(new PlcPointInfoModel { Address = address, Name = name, DataType = dataType, IsReadOnly = isReadOnly });
        public IEnumerable<PlcPointInfoModel> GetAllRegisteredPoints() => points;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private short ReadInt16(string address) => Int16Reads.GetValueOrDefault(address);
    }

    private sealed class FakeDeviceConfig : IDeviceConfig
    {
        public bool Validate() => true;
    }
}

