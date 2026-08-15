using System.Threading.Channels;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow;

/// <summary>
/// 工位实时采集结果队列。实时线程只负责 TryWrite，后台单消费者负责 UI、统计、保存等非实时工作。
/// </summary>
internal sealed class StationResultDispatchQueue
{
    private readonly Channel<StationResultMessage> channel = Channel.CreateUnbounded<StationResultMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public bool TryEnqueue(StationResultMessage message)
        => channel.Writer.TryWrite(message);

    public async Task RunAsync(MachineBase machine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(machine);

        await foreach (StationResultMessage message in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await machine.ProcessStationResultAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Complete()
        => channel.Writer.TryComplete();
}

internal sealed class StationResultMessage
{
    private readonly IReadOnlyList<CapturedStationDataDeal>? captures;

    private StationResultMessage(
        TestStationModel station,
        IReadOnlyList<StationResultValue> values,
        bool isPass,
        IReadOnlyList<CapturedStationDataDeal>? captures = null)
    {
        Station = station;
        Values = values;
        IsPass = isPass;
        this.captures = captures;
    }

    public TestStationModel Station { get; }

    public IReadOnlyList<StationResultValue> Values { get; private set; }

    public bool IsPass { get; private set; }

    public static StationResultMessage Create(TestStationModel station)
        => CreateFromStation(station);

    public static StationResultMessage CreateDeferredHardware(
        TestStationModel station,
        bool hardwareResult,
        IReadOnlyList<CapturedStationDataDeal> captures)
        => new(station, [], hardwareResult, captures);

    public void ApplyToStation()
    {
        if (captures != null)
        {
            foreach (CapturedStationDataDeal captured in captures)
            {
                captured.Deal.ApplyCapture(captured.Capture, IsPass, Station);
            }

            StationResultMessage resolved = CreateFromStation(Station);
            Values = resolved.Values;
            IsPass = resolved.IsPass;
            return;
        }

        foreach (StationResultValue value in Values)
        {
            Station.TestValues[value.TestName] = value.Value;
            if (value.Judge.HasValue)
            {
                Station.TestJudges[value.TestName] = value.Judge.Value;
            }
        }
    }

    private static StationResultMessage CreateFromStation(TestStationModel station)
    {
        var values = new List<StationResultValue>();
        var testNames = new List<string>(station.OrderedTestNames);
        if (station.ShowInResultGrid)
        {
            foreach (string testName in station.TestValues.Keys)
            {
                if (!testNames.Contains(testName, StringComparer.OrdinalIgnoreCase))
                {
                    testNames.Add(testName);
                }
            }
        }

        foreach (string testName in testNames)
        {
            if (station.TestValues.TryGetValue(testName, out double value))
            {
                values.Add(new StationResultValue(
                    testName,
                    value,
                    station.TestJudges.TryGetValue(testName, out bool ok) ? ok : null));
            }
        }

        bool isPass = station.TestJudges.Count == 0 || station.TestJudges.All(static pair => pair.Value);
        return new StationResultMessage(station, values, isPass);
    }
}

internal sealed record CapturedStationDataDeal(IStationDataDeal Deal, IStationDataCapture Capture);

internal sealed record StationResultValue(string TestName, double Value, bool? Judge);
