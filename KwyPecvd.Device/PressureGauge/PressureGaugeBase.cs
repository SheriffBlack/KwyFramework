namespace KwyPecvd.Device.PressureGauge;

public abstract class PressureGaugeBase : HardwareComponentBase, IPressureGauge
{
    private readonly object syncRoot = new();

    private PressureGaugeSnapshot snapshot;

    protected PressureGaugeBase(PressureGaugeDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        snapshot = new PressureGaugeSnapshot
        {
            Id = definition.Id,
            Pressure = 0,
            Unit = definition.Unit,
            IsOffline = true,
            IsReadingValid = false,
            DiagnosticCode = PressureGaugeDiagnosticCodes.NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public PressureGaugeDefinition Definition { get; }

    public PressureGaugeSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<PressureGaugeSnapshot>? SnapshotChanged;

    public sealed override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Definition.Validate();
        await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public sealed override Task ShutdownAsync(CancellationToken cancellationToken = default) => ShutdownCoreAsync(cancellationToken);

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public sealed override async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await ReadPressureAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            var readingInRange =
                double.IsFinite(result.Pressure) &&
                result.Pressure >= Definition.Minimum &&
                result.Pressure <= Definition.Maximum;

            var readingValid =
                !result.IsOffline &&
                result.IsReadingValid &&
                readingInRange;

            var diagnosticCode =
                result.IsOffline
                    ? PressureGaugeDiagnosticCodes
                        .Offline
                    : !readingInRange
                        ? PressureGaugeDiagnosticCodes
                            .ReadingOutOfRange
                        : !result.IsReadingValid
                            ? PressureGaugeDiagnosticCodes
                                .ReadingBadQuality
                            : result.DiagnosticCode;

            var current =
                new PressureGaugeSnapshot
                {
                    Id = Definition.Id,
                    Pressure = result.Pressure,
                    Unit = Definition.Unit,
                    IsOffline = result.IsOffline,
                    IsReadingValid = readingValid,
                    DiagnosticCode = diagnosticCode,
                    Timestamp = result.Timestamp
                };

            UpdateSnapshot(current);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
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
                    PressureGaugeDiagnosticCodes
                        .ReadFailed,
                Timestamp = DateTimeOffset.UtcNow
            };

            UpdateSnapshot(failed);
            throw;
        }
    }

    private void UpdateSnapshot(PressureGaugeSnapshot current)
    {
        lock (syncRoot)
        {
            snapshot = current;
        }

        PublishSnapshotChanged(current);
    }

    private void PublishSnapshotChanged(PressureGaugeSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<
                PressureGaugeSnapshot>>()
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
                // 后续接入统一日志。
            }
        }
    }

    protected abstract ValueTask<PressureGaugeCycleResult> ReadPressureAsync(CancellationToken cancellationToken);

    protected readonly record struct PressureGaugeCycleResult(
        double Pressure,
        bool IsOffline,
        bool IsReadingValid,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}

