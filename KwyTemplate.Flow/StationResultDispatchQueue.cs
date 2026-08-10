using System.Threading.Channels;
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

internal sealed record StationResultMessage(
    TestStationModel Station,
    IReadOnlyList<StationResultValue> Values,
    bool IsPass)
{
    public void ApplyToStation()
    {
        foreach (StationResultValue value in Values)
        {
            Station.TestValues[value.TestName] = value.Value;
            if (value.Judge.HasValue)
            {
                Station.TestJudges[value.TestName] = value.Judge.Value;
            }
        }
    }
}

internal sealed record StationResultValue(string TestName, double Value, bool? Judge);