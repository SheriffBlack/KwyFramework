using Kwy.Device.IoCard.Abstractions;
using KwyPecvd.Device.Thermal.TemperatureSensor;

namespace KwyPecvd.Device.Thermal.TemperatureSensor.Io;

public sealed class IoTemperatureSensor : TemperatureSensorBase
{
    private readonly IoTemperatureSensorDefinition
        ioDefinition;

    private readonly IAnalogIoPointDefinitionProvider
        analogPointProvider;

    private readonly ILogicalAnalogInputReader
        analogReader;

    private readonly IDigitalIoPointDefinitionProvider?
        digitalPointProvider;

    private readonly ILogicalDigitalInputReader?
        digitalReader;

    public IoTemperatureSensor(
        IoTemperatureSensorDefinition definition,
        IAnalogIoPointDefinitionProvider analogPointProvider,
        ILogicalAnalogInputReader analogReader,
        IDigitalIoPointDefinitionProvider? digitalPointProvider = null,
        ILogicalDigitalInputReader? digitalReader = null)
        : base(
            definition?.Sensor ??
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
        cancellationToken.ThrowIfCancellationRequested();

        ioDefinition.Validate();

        var temperaturePoint =
            analogPointProvider.GetRequired(
                ioDefinition.TemperatureAiId);

        ValidateTemperaturePoint(
            temperaturePoint);

        ValidateOfflinePoint();

        return Task.CompletedTask;
    }

    protected override async ValueTask<TemperatureSensorCycleResult> ReadTemperatureAsync(
        CancellationToken cancellationToken)
    {
        var sample =
            await analogReader.ReadSampleAsync(
                    ioDefinition.TemperatureAiId,
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
                ? TemperatureSensorDiagnosticCodes
                    .Offline
                : readingValid
                    ? null
                    : TemperatureSensorDiagnosticCodes
                        .ReadingBadQuality;

        return new TemperatureSensorCycleResult(
            Temperature: sample.Value,
            IsOffline: offlineByDi,
            IsReadingValid: readingValid,
            Timestamp: sample.Timestamp,
            DiagnosticCode: diagnosticCode);
    }

    private void ValidateTemperaturePoint(
    AnalogIoPointDefinition point)
    {
        if (point.Direction !=
            AnalogIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Temperature sensor {Id} point " +
                $"'{point.Id}' must be an " +
                "analog input.");
        }

        if (!string.Equals(
                point.Unit,
                Definition.Unit,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Temperature sensor {Id} point " +
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
                $"Temperature sensor {Id} point " +
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
                $"Temperature sensor {Id} defines " +
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
                $"Temperature sensor {Id} offline " +
                $"point '{point.Id}' must be a " +
                "digital input.");
        }
    }
}