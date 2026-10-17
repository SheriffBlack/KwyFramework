namespace KwyPecvd.Device.Thermal.TemperatureSensor;

public abstract class TemperatureSensorBase : HardwareComponentBase, ITemperatureSensor
{
    private readonly object syncRoot = new();

    private TemperatureSensorSnapshot snapshot;

    protected TemperatureSensorBase(TemperatureSensorDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        snapshot = new TemperatureSensorSnapshot
        {
            Id = definition.Id,
            Temperature = 0,
            Unit = definition.Unit,
            IsOffline = true,
            IsReadingValid = false,
            DiagnosticCode =
                TemperatureSensorDiagnosticCodes
                    .NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public TemperatureSensorDefinition Definition { get; }

    public TemperatureSensorSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<TemperatureSensorSnapshot>? SnapshotChanged;

    public override sealed async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override sealed Task ShutdownAsync(CancellationToken cancellationToken = default) => ShutdownCoreAsync(cancellationToken);

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override sealed async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 读取原始结果
            var result = await ReadTemperatureAsync(cancellationToken).ConfigureAwait(false);

            // 对温度做业务范围校验
            var readingInRange =
                double.IsFinite(result.Temperature) &&
                result.Temperature >=
                    Definition.Minimum &&
                result.Temperature <=
                    Definition.Maximum;

            // 综合判断反馈是否有效
            var readingValid =
                !result.IsOffline &&
                result.IsReadingValid &&
                readingInRange;

            // 统一诊断原因
            var diagnosticCode =
                result.IsOffline
                    ? TemperatureSensorDiagnosticCodes                    // 离线                → Offline
                        .Offline                                          // 数值越界            → ReadingOutOfRange
                    : !readingInRange                                     // 底层读取质量无效    → ReadingBadQuality
                        ? TemperatureSensorDiagnosticCodes                // 其他驱动特有问题    → result.DiagnosticCode
                            .ReadingOutOfRange                            // 正常                → null
                        : !result.IsReadingValid
                            ? TemperatureSensorDiagnosticCodes
                                .ReadingBadQuality
                            : result.DiagnosticCode;

            // 生成对外快照
            var current = new TemperatureSensorSnapshot
            {
                Id = Definition.Id,
                Temperature =
                    result.Temperature,
                Unit = Definition.Unit,
                IsOffline = result.IsOffline,
                IsReadingValid = readingValid,
                DiagnosticCode =
                    diagnosticCode,
                Timestamp = result.Timestamp
            };

            UpdateSnapshot(current);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            var failed = Snapshot with
            {
                IsOffline = true,
                IsReadingValid = false,
                DiagnosticCode =
                    TemperatureSensorDiagnosticCodes
                        .ReadFailed,
                Timestamp = DateTimeOffset.UtcNow
            };

            UpdateSnapshot(failed);
            throw;
        }
    }

    protected abstract ValueTask<TemperatureSensorCycleResult> ReadTemperatureAsync(CancellationToken cancellationToken);

    private void UpdateSnapshot(TemperatureSensorSnapshot current)
    {
        lock (syncRoot)
        {
            snapshot = current;
        }

        PublishSnapshotChanged(current);
    }

    private void PublishSnapshotChanged(TemperatureSensorSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<
                TemperatureSensorSnapshot>>()
            .ToArray();

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(this, current);
            }
            catch
            {
                // 观察者异常不能中断硬件周期。
            }
        }
    }

    protected readonly record struct TemperatureSensorCycleResult(
        double Temperature,
        bool IsOffline,
        bool IsReadingValid,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}