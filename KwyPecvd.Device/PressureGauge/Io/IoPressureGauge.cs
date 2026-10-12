using Kwy.Device.IoCard.Abstractions;
using KwyPecvd.Device.PressureGauge;

namespace KwyPecvd.Device.PressureGauge.Io;

public sealed class IoPressureGauge :PressureGaugeBase
{
    private readonly IoPressureGaugeDefinition
        ioDefinition;

    private readonly
        IAnalogIoPointDefinitionProvider
        analogPointProvider;

    private readonly ILogicalAnalogInputReader
        analogReader;

    private readonly
        IDigitalIoPointDefinitionProvider?
        digitalPointProvider;

    private readonly ILogicalDigitalInputReader?
        digitalReader;

    public IoPressureGauge(
        IoPressureGaugeDefinition definition,
        IAnalogIoPointDefinitionProvider
            analogPointProvider,
        ILogicalAnalogInputReader analogReader,
        IDigitalIoPointDefinitionProvider?
            digitalPointProvider = null,
        ILogicalDigitalInputReader?
            digitalReader = null)
        : base(
            definition?.PressureGauge ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        ioDefinition = definition;

        this.analogPointProvider =
            analogPointProvider ??
            throw new ArgumentNullException(
                nameof(analogPointProvider));

        this.analogReader =
            analogReader ??
            throw new ArgumentNullException(
                nameof(analogReader));

        this.digitalPointProvider =
            digitalPointProvider;

        this.digitalReader = digitalReader;
    }

    protected override Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        ioDefinition.Validate();

        var pressurePoint =
            analogPointProvider.GetRequired(
                ioDefinition.PressureAiId);

        ValidatePressurePoint(pressurePoint);

        ValidateOfflinePoint();

        return Task.CompletedTask;
    }

    protected override async ValueTask<
        PressureGaugeCycleResult>
        ReadPressureAsync(
            CancellationToken cancellationToken)
    {
        var sample =
            await analogReader.ReadSampleAsync(
                ioDefinition.PressureAiId,
                cancellationToken)
            .ConfigureAwait(false);

        var offlineByDi =
            ioDefinition.OfflineDiId is not null &&
            digitalReader!.ReadDi(
                ioDefinition.OfflineDiId);

        var readingValid =
            !offlineByDi &&
            sample.Quality ==
                AnalogIoQuality.Good;

        var diagnosticCode =
            offlineByDi
                ? PressureGaugeDiagnosticCodes
                    .Offline
                : readingValid
                    ? null
                    : PressureGaugeDiagnosticCodes
                        .ReadingBadQuality;

        return new PressureGaugeCycleResult(
            Pressure: sample.Value,
            IsOffline: offlineByDi,
            IsReadingValid: readingValid,
            Timestamp: sample.Timestamp,
            DiagnosticCode: diagnosticCode);
    }

    private void ValidatePressurePoint(
        AnalogIoPointDefinition point)
    {
        if (point.Direction !=
            AnalogIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} point " +
                $"'{point.Id}' must be an " +
                "analog input.");
        }

        if (!string.Equals(
                point.Unit,
                Definition.Unit,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} point " +
                $"'{point.Id}' uses unit " +
                $"'{point.Unit}', expected " +
                $"'{Definition.Unit}'.");
        }

        if (point.Scale.EngineeringMinimum !=
                Definition.Minimum ||
            point.Scale.EngineeringMaximum !=
                Definition.Maximum)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} point " +
                $"'{point.Id}' engineering range " +
                $"must be " +
                $"{Definition.Minimum}~" +
                $"{Definition.Maximum} " +
                $"{Definition.Unit}.");
        }
    }

    private void ValidateOfflinePoint()
    {
        if (ioDefinition.OfflineDiId is null)
        {
            return;
        }

        if (digitalPointProvider is null ||
            digitalReader is null)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} defines " +
                $"offline DI " +
                $"'{ioDefinition.OfflineDiId}', " +
                "but digital IO services were " +
                "not supplied.");
        }

        var point =
            digitalPointProvider.GetRequired(
                ioDefinition.OfflineDiId);

        if (point.Direction !=
            DigitalIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Pressure gauge {Id} offline " +
                $"point '{point.Id}' must be a " +
                "digital input.");
        }
    }
}
