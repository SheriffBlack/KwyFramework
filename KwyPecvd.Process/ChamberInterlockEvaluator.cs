using KwyPecvd.Device;
using KwyPecvd.Process.Chamber.Interlock;

namespace KwyPecvd.Process;

/// <summary>
/// 解释规则：这些条件是否允许气体准备
/// </summary>
internal sealed class ChamberInterlockEvaluator
{
    private readonly string chamberId;
    private readonly IChamberInterlockSource source;

    public ChamberInterlockEvaluator(
        string chamberId,
        IChamberInterlockSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            chamberId);

        ArgumentNullException.ThrowIfNull(source);

        this.chamberId = chamberId;
        this.source = source;
    }

    public bool TryValidateGasPreparation(
        out CommandResult failure)
    {
        var snapshot = source.Snapshot;

        if (!snapshot.IsValid)
        {
            failure = CommandResult.Failure(
                ResultCodes.Interlock,
                $"Chamber '{chamberId}' interlock " +
                "feedback is invalid.");
            return false;
        }

        if (!snapshot.EmergencyStopHealthy)
        {
            failure = CommandResult.Failure(
                ResultCodes.Interlock,
                $"Chamber '{chamberId}' emergency " +
                "stop circuit is not healthy.");
            return false;
        }

        if (!snapshot.DoorClosed)
        {
            failure = CommandResult.Failure(
                ResultCodes.Interlock,
                $"Chamber '{chamberId}' door " +
                "is not closed.");
            return false;
        }

        if (!snapshot.VacuumReady)
        {
            failure = CommandResult.Failure(
                ResultCodes.Interlock,
                $"Chamber '{chamberId}' vacuum " +
                "is not ready.");
            return false;
        }

        failure = CommandResult.Success();
        return true;
    }
}