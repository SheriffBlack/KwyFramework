using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class CompositeDeviceStateSynchronizer : IDeviceStateSynchronizer
{
    private readonly IEnumerable<IDeviceStateParticipant> participants;

    public CompositeDeviceStateSynchronizer(IEnumerable<IDeviceStateParticipant> participants)
    {
        this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
    }

    public async Task<DeviceSyncResult> SyncStateAsync(CancellationToken cancellationToken = default)
    {
        IDeviceStateParticipant[] currentParticipants = participants.ToArray();
        if (currentParticipants.Length == 0)
        {
            return new DeviceSyncResult(
                DeviceSyncState.Unknown,
                Array.Empty<DeviceSyncItem>(),
                "No device state synchronizer is configured.");
        }

        var items = new List<DeviceSyncItem>();
        var messages = new List<string>();
        DeviceSyncState worstState = DeviceSyncState.Synchronized;

        foreach (IDeviceStateParticipant participant in currentParticipants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSyncResult result = await participant.SyncStateAsync(cancellationToken).ConfigureAwait(false);

            items.Add(new DeviceSyncItem($"{participant.DeviceId}.State", result.State.ToString()));
            foreach (DeviceSyncItem item in result.Items)
            {
                items.Add(new DeviceSyncItem($"{participant.DeviceId}.{item.Name}", item.Value));
            }

            if (!result.IsReady)
            {
                worstState = SelectWorseState(worstState, result.State);
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    messages.Add($"{participant.DeviceId}: {result.Message}");
                }
            }
        }

        return worstState == DeviceSyncState.Synchronized
            ? DeviceSyncResult.Synchronized(items)
            : new DeviceSyncResult(worstState, items, string.Join("; ", messages));
    }

    private static DeviceSyncState SelectWorseState(DeviceSyncState current, DeviceSyncState candidate)
    {
        return Rank(candidate) > Rank(current) ? candidate : current;
    }

    private static int Rank(DeviceSyncState state) => state switch
    {
        DeviceSyncState.Synchronized => 0,
        DeviceSyncState.Unknown => 1,
        DeviceSyncState.NotReady => 2,
        DeviceSyncState.Offline => 3,
        DeviceSyncState.Alarm => 4,
        DeviceSyncState.Failed => 5,
        _ => 1
    };
}
