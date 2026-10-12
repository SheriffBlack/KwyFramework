using Kwy.Device.IoCard.Abstractions;

namespace KwyPecvd.Device.Thermal
    .TemperatureController.Io;

public sealed class IoTemperatureController : TemperatureControllerBase
{
    private readonly IoTemperatureControllerDefinition ioDefinition;

    private readonly IAnalogIoPointDefinitionProvider analogPointProvider;

    private readonly IDigitalIoPointDefinitionProvider digitalPointProvider;

    private readonly ILogicalAnalogInputReader analogReader;

    private readonly ILogicalAnalogOutputWriter analogWriter;

    private readonly ILogicalDigitalInputReader digitalReader;

    private readonly ILogicalDigitalOutputWriter digitalWriter;

    public IoTemperatureController(
        IoTemperatureControllerDefinition definition,
        IAnalogIoPointDefinitionProvider analogPointProvider,
        IDigitalIoPointDefinitionProvider digitalPointProvider,
        ILogicalAnalogInputReader analogReader,
        ILogicalAnalogOutputWriter analogWriter,
        ILogicalDigitalInputReader digitalReader,
        ILogicalDigitalOutputWriter digitalWriter)
        : base(
            definition?.Controller ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        ioDefinition = definition;

        this.analogPointProvider =
            analogPointProvider ??
            throw new ArgumentNullException(
                nameof(analogPointProvider));

        this.digitalPointProvider =
            digitalPointProvider ??
            throw new ArgumentNullException(
                nameof(digitalPointProvider));

        this.analogReader =
            analogReader ??
            throw new ArgumentNullException(
                nameof(analogReader));

        this.analogWriter =
            analogWriter ??
            throw new ArgumentNullException(
                nameof(analogWriter));

        this.digitalReader =
            digitalReader ??
            throw new ArgumentNullException(
                nameof(digitalReader));

        this.digitalWriter =
            digitalWriter ??
            throw new ArgumentNullException(
                nameof(digitalWriter));
    }

    protected override async Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ioDefinition.Validate();

        ValidateSetPointOutput();
        ValidateFeedbackInput();
        ValidateEnableOutput();

        ValidateOptionalInput(
            ioDefinition.PowerOnDiId);

        ValidateOptionalInput(
            ioDefinition.SensorFaultDiId);

        ValidateOptionalInput(
            ioDefinition.OfflineDiId);

        // 初始化必须先撤销加热允许。
        WriteEnable(false);

        await WriteSetPointAsync(
                Definition.MinimumSetPoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected override async ValueTask<TemperatureControllerCycleResult> ExchangeAsync(
        double target,
        bool enabled,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!enabled)
            {
                // 禁用方向优先撤销DO。
                WriteEnable(false);

                await WriteSetPointAsync(
                        Definition.MinimumSetPoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // 启用前先准备目标AO，
                // 最后才允许接通加热DO。
                await WriteSetPointAsync(
                        target,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var sample =
                await analogReader.ReadSampleAsync(
                        ioDefinition.FeedbackAiId,
                        cancellationToken)
                    .ConfigureAwait(false);

            var offline =
                ReadOptional(
                    ioDefinition.OfflineDiId);

            var sensorFault =
                ReadOptional(
                    ioDefinition.SensorFaultDiId);

            var feedbackValid =
                !offline &&
                !sensorFault &&
                sample.Quality ==
                    AnalogIoQuality.Good;

            var effectiveEnabled =
                enabled &&
                feedbackValid;

            if (enabled)
            {
                // 只有反馈和在线状态正常时，
                // 才真正接通加热允许。
                WriteEnable(effectiveEnabled);
            }

            var powerOn =
                ioDefinition.PowerOnDiId is not null
                    ? digitalReader.ReadDi(
                        ioDefinition.PowerOnDiId)
                    : ioDefinition
                        .UseEnableCommandAsPowerFeedback &&
                      effectiveEnabled;

            var diagnosticCode =
                sensorFault
                    ? TemperatureControllerDiagnosticCodes
                        .SensorFault
                    : sample.Quality !=
                        AnalogIoQuality.Good
                        ? TemperatureControllerDiagnosticCodes
                            .FeedbackBadQuality
                        : null;

            return new TemperatureControllerCycleResult(
                Feedback: sample.Value,
                IsPowerOn: powerOn,
                IsOffline: offline,
                IsFeedbackValid: feedbackValid,
                Timestamp: sample.Timestamp,
                DiagnosticCode: diagnosticCode);
        }
        catch (Exception exchangeException)
        {
            try
            {
                WriteEnable(false);
            }
            catch (Exception safetyException)
            {
                throw new AggregateException(
                    $"Temperature controller '{Id}' " +
                    "exchange failed and the heating " +
                    "enable output could not be cleared.",
                    exchangeException,
                    safetyException);
            }

            throw;
        }
    }
    private ValueTask WriteSetPointAsync(
        double value,
        CancellationToken cancellationToken)
    {
        return analogWriter.WriteAsync(
            ioDefinition.SetPointAoId,
            value,
            ioDefinition.OutputOwner,
            cancellationToken);
    }

    private void WriteEnable(bool enabled)
    {
        digitalWriter.WriteDo(
            ioDefinition.EnableDoId,
            enabled,
            ioDefinition.OutputOwner);
    }

    private void ValidateSetPointOutput()
    {
        var point =
            analogPointProvider.GetRequired(
                ioDefinition.SetPointAoId);

        if (point.Direction !=
            AnalogIoDirection.Output)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"set-point '{point.Id}' must be " +
                "an analog output.");
        }

        if (!string.Equals(
                point.Unit,
                Definition.Unit,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"set-point '{point.Id}' uses unit " +
                $"'{point.Unit}', expected " +
                $"'{Definition.Unit}'.");
        }

        if (point.Scale.EngineeringMinimum >
                Definition.MinimumSetPoint ||
            point.Scale.EngineeringMaximum <
                Definition.MaximumSetPoint)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"set-point '{point.Id}' does not " +
                "cover configured range " +
                $"{Definition.MinimumSetPoint}~" +
                $"{Definition.MaximumSetPoint} " +
                $"{Definition.Unit}.");
        }

        if (point.ProcessSafeValue !=
            Definition.MinimumSetPoint)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"set-point '{point.Id}' process " +
                $"safe value must be " +
                $"{Definition.MinimumSetPoint} " +
                $"{Definition.Unit}.");
        }
    }

    private void ValidateFeedbackInput()
    {
        var point =
            analogPointProvider.GetRequired(
                ioDefinition.FeedbackAiId);

        if (point.Direction !=
            AnalogIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"feedback '{point.Id}' must be " +
                "an analog input.");
        }

        if (!string.Equals(
                point.Unit,
                Definition.Unit,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"feedback '{point.Id}' uses unit " +
                $"'{point.Unit}', expected " +
                $"'{Definition.Unit}'.");
        }
    }

    private void ValidateEnableOutput()
    {
        var point =
            digitalPointProvider.GetRequired(
                ioDefinition.EnableDoId);

        if (point.Direction !=
            DigitalIoDirection.Output)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"enable '{point.Id}' must be a " +
                "digital output.");
        }

        if (point.ProcessSafeState != false)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"enable '{point.Id}' process " +
                "safe state must be false.");
        }
    }

    private void ValidateOptionalInput(
        string? pointId)
    {
        if (pointId is null)
        {
            return;
        }

        var point =
            digitalPointProvider.GetRequired(pointId);

        if (point.Direction !=
            DigitalIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Temperature controller {Id} " +
                $"point '{point.Id}' must be a " +
                "digital input.");
        }
    }

    private bool ReadOptional(
        string? pointId,
        bool defaultValue = false)
    {
        return pointId is null
            ? defaultValue
            : digitalReader.ReadDi(pointId);
    }
}