using KwyPecvd.Process.Chambers;

namespace KwyPecvd.RT.Simulated;

public sealed class SimulatedChamberInterlockSource: IChamberInterlockSource
{
    public ChamberInterlockSnapshot Snapshot
    {
        get;
        set;
    } = new()
    {
        IsValid = true,
        EmergencyStopHealthy = true,
        DoorClosed = true,
        VacuumReady = true,
        Timestamp = DateTimeOffset.UtcNow
    };
}
