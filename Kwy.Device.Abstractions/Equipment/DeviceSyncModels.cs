namespace Kwy.Device.Abstractions.Equipment;

public enum DeviceSyncState
{
    Unknown,
    Synchronized,
    NotReady,
    Alarm,
    Offline,
    Failed
}

public sealed record DeviceSyncItem(string Name, string Value);

public sealed record DeviceSyncResult(
    DeviceSyncState State,
    IReadOnlyList<DeviceSyncItem> Items,
    string? Message = null)
{
    public static DeviceSyncResult Synchronized(IReadOnlyList<DeviceSyncItem>? items = null)
        => new(DeviceSyncState.Synchronized, items ?? Array.Empty<DeviceSyncItem>());

    public static DeviceSyncResult Failed(string message)
        => new(DeviceSyncState.Failed, Array.Empty<DeviceSyncItem>(), message);

    public bool IsReady => State == DeviceSyncState.Synchronized;
}
