using Kwy.Device.IoCard.Abstractions;

namespace KwyPecvd.Device.Mfc.Io;

public sealed class IoMfc : MfcBase
{
    private readonly IoMfcDefinition ioDefinition;
    private readonly IAnalogIoPointDefinitionProvider analogPointProvider;
    private readonly IDigitalIoPointDefinitionProvider? digitalPointProvider;
    private readonly ILogicalAnalogInputReader analogReader;
    private readonly ILogicalAnalogOutputWriter analogWriter;
    private readonly ILogicalDigitalInputReader? digitalReader;

    public IoMfc(
        IoMfcDefinition definition,
        IAnalogIoPointDefinitionProvider analogPointProvider,
        ILogicalAnalogInputReader analogReader,
        ILogicalAnalogOutputWriter analogWriter,
        IDigitalIoPointDefinitionProvider? digitalPointProvider = null,
        ILogicalDigitalInputReader? digitalReader = null)
        : base(definition?.Mfc ?? throw new ArgumentNullException(nameof(definition)), initiallyOffline: true)
    {
        definition.Validate();
        ioDefinition = definition;
        this.analogPointProvider = analogPointProvider ?? throw new ArgumentNullException(nameof(analogPointProvider));
        this.analogReader = analogReader ?? throw new ArgumentNullException(nameof(analogReader));
        this.analogWriter = analogWriter ?? throw new ArgumentNullException(nameof(analogWriter));
        this.digitalPointProvider = digitalPointProvider;
        this.digitalReader = digitalReader;
    }

    protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ioDefinition.Validate();

        var setPoint = analogPointProvider.GetRequired(ioDefinition.SetPointAoId);
        ValidateAnalogPoint(setPoint, AnalogIoDirection.Output, "set-point");

        var feedback = analogPointProvider.GetRequired(ioDefinition.FeedbackAiId);
        ValidateAnalogPoint(feedback, AnalogIoDirection.Input, "feedback");

        if (ioDefinition.OfflineDiId is not null)
        {
            if (digitalPointProvider is null || digitalReader is null)
                throw new InvalidOperationException(
                    $"MFC {Id} defines offline DI '{ioDefinition.OfflineDiId}', but digital IO services were not supplied.");

            var offline = digitalPointProvider.GetRequired(ioDefinition.OfflineDiId);
            if (offline.Direction != DigitalIoDirection.Input)
                throw new InvalidOperationException(
                    $"MFC {Id} offline point '{offline.Id}' must be a digital input.");
        }

        return Task.CompletedTask;
    }

    protected override async ValueTask<MfcCycleResult> ExchangeAsync(
        double setPoint,
        CancellationToken cancellationToken)
    {
        await analogWriter.WriteAsync(
            ioDefinition.SetPointAoId,
            setPoint,
            ioDefinition.OutputOwner,
            cancellationToken).ConfigureAwait(false);

        AnalogIoSample sample = await analogReader.ReadSampleAsync(
            ioDefinition.FeedbackAiId,
            cancellationToken).ConfigureAwait(false);

        var offlineByDi =
            ioDefinition.OfflineDiId is not null &&
            digitalReader!.ReadDi(
                ioDefinition.OfflineDiId);

        var feedbackValid =
            !offlineByDi &&
            sample.Quality == AnalogIoQuality.Good;

        var diagnosticCode =
            offlineByDi
                ? MfcDiagnosticCodes.Offline
                : feedbackValid
                    ? null
                    : MfcDiagnosticCodes.FeedbackBadQuality;

        return new MfcCycleResult(
            Feedback: sample.Value,
            IsOffline: offlineByDi,
            IsFeedbackValid: feedbackValid,
            Timestamp: sample.Timestamp,
            DiagnosticCode: diagnosticCode);
    }

    private void ValidateAnalogPoint(
        AnalogIoPointDefinition point,
        AnalogIoDirection expectedDirection,
        string role)
    {
        if (point.Direction != expectedDirection)
            throw new InvalidOperationException(
                $"MFC {Id} {role} point '{point.Id}' must be {expectedDirection}.");
        if (point.Scale.EngineeringMinimum != 0 ||
            point.Scale.EngineeringMaximum != Definition.FullScale)
            throw new InvalidOperationException(
                $"MFC {Id} {role} point '{point.Id}' engineering range must be " +
                $"0~{Definition.FullScale} {Definition.Unit}.");
        if (!string.Equals(point.Unit, Definition.Unit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"MFC {Id} {role} point '{point.Id}' uses unit '{point.Unit}', " +
                $"expected '{Definition.Unit}'.");
    }
}
