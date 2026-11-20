using KwyPecvd.Equipment.Commands;
using Microsoft.Extensions.Options;

namespace KwyPecvd.RT.Runtime;

/// <summary>
/// 低频清理超过保留时间的Equipment Command终态结果。
/// </summary>
public sealed class EquipmentCommandResultRetentionService : BackgroundService
{
    private readonly IEquipmentCommandResultStore resultStore;
    private readonly EquipmentCommandResultRetentionOptions options;
    private readonly ILogger<EquipmentCommandResultRetentionService> logger;

    public EquipmentCommandResultRetentionService(
        IEquipmentCommandResultStore resultStore,
        IOptions<EquipmentCommandResultRetentionOptions> options,
        ILogger<EquipmentCommandResultRetentionService> logger)
    {
        this.resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));

        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;

        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cutoff = DateTimeOffset.UtcNow - options.RetentionPeriod;

            var removedCount = resultStore.RemoveTerminalResultsOlderThan(cutoff);

            if (removedCount > 0)
            {
                logger.LogInformation(
                    "Removed {RemovedCount} expired " +
                    "equipment command results older than {Cutoff}.",
                    removedCount,
                    cutoff);
            }
        }
    }
}
