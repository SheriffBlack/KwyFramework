using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Device.VacuumPump.Io;

public sealed record IoVacuumPumpDefinition
{
    public required VacuumPumpDefinition Pump { get; init; }

    /// <summary>持续型运行命令输出，而不是启动脉冲。</summary>
    public required string RunCommandDoId { get; init; }

    /// <summary>泵正在运行反馈。</summary>
    public required string RunningDiId { get; init; }

    public string? WarningDiId { get; init; }

    public string? AlarmDiId { get; init; }

    public string? OverloadDiId { get; init; }

    public string? N2ReadyDiId { get; init; }

    public string? ExhaustReadyDiId { get; init; }

    public string? OfflineDiId { get; init; }

    /// <summary>写入DO时使用的所有者名称。</summary>
    public string? OutputOwner { get; init; }

    public void Validate()
    {
        Pump.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            RunCommandDoId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            RunningDiId);
    }
}