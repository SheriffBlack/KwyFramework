using Kwy.Device.IoCard.Abstractions;

namespace KwyPecvd.Device.OnOffValve.Io;

public sealed class IoOnOffValve :
    OnOffValveBase
{
    private readonly IoOnOffValveDefinition
        ioDefinition;

    private readonly IDigitalIoPointDefinitionProvider
        pointProvider;

    private readonly ILogicalDigitalInputReader
        inputReader;

    private readonly ILogicalDigitalOutputWriter
        outputWriter;

    public IoOnOffValve(
        IoOnOffValveDefinition definition,
        IDigitalIoPointDefinitionProvider pointProvider,
        ILogicalDigitalInputReader inputReader,
        ILogicalDigitalOutputWriter outputWriter)
        : base(
            definition?.Valve ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        ioDefinition = definition;

        this.pointProvider =
            pointProvider ??
            throw new ArgumentNullException(
                nameof(pointProvider));

        this.inputReader =
            inputReader ??
            throw new ArgumentNullException(
                nameof(inputReader));

        this.outputWriter =
            outputWriter ??
            throw new ArgumentNullException(
                nameof(outputWriter));
    }

    protected override Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ioDefinition.Validate();

        ValidateOutput(
            ioDefinition.OpenDoId,
            expectedSafeState:
                Definition.SafePosition ==
                OnOffValvePosition.Open);

        if (ioDefinition.CloseDoId is not null)
        {
            ValidateOutput(
                ioDefinition.CloseDoId,
                expectedSafeState:
                    Definition.SafePosition ==
                    OnOffValvePosition.Closed);
        }

        ValidateOptionalInput(
            ioDefinition.OpenDiId);

        ValidateOptionalInput(
            ioDefinition.ClosedDiId);

        ValidateOptionalInput(
            ioDefinition.OfflineDiId);

        // 初始化时立即收敛到工艺安全位置。
        WritePosition(Definition.SafePosition);

        return Task.CompletedTask;
    }

    protected override ValueTask<
    OnOffValveCycleResult>
    ExchangeAsync(
        OnOffValvePosition commandedPosition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safePosition = Definition.SafePosition;

        try
        {
            // 安全方向命令优先执行。
            // 即使后续读取反馈失败，也已写入安全输出。
            if (commandedPosition == safePosition)
            {
                WritePosition(safePosition);
            }

            var offline =
                ReadOptional(
                    ioDefinition.OfflineDiId);

            // 非安全方向只有在线时才允许输出。
            var effectivePosition =
                offline
                    ? safePosition
                    : commandedPosition;

            if (commandedPosition != safePosition)
            {
                WritePosition(effectivePosition);
            }

            var feedback =
                ReadPositionFeedback(
                    effectivePosition);

            var feedbackValid =
                !offline &&
                !feedback.IsConflict;

            return ValueTask.FromResult(
                new OnOffValveCycleResult(
                    ActualPosition:
                        feedback.Position,
                    IsOffline: offline,
                    IsFeedbackValid:
                        feedbackValid,
                    IsFeedbackConflict:
                        feedback.IsConflict,
                    Timestamp:
                        DateTimeOffset.UtcNow));
        }
        catch
        {
            // 任意IO异常都尽力恢复安全输出。
            WritePosition(safePosition);
            throw;
        }
    }

    private void WritePosition(
        OnOffValvePosition position)
    {
        var open =
            position == OnOffValvePosition.Open;

        if (ioDefinition.CloseDoId is null)
        {
            // 单DO：true打开，false关闭。
            outputWriter.WriteDo(
                ioDefinition.OpenDoId,
                open,
                ioDefinition.OutputOwner);

            return;
        }

        if (open)
        {
            // 双DO开阀：先撤销关闭输出，
            // 避免两个线圈同时得电。
            outputWriter.WriteDo(
                ioDefinition.CloseDoId,
                false,
                ioDefinition.OutputOwner);

            outputWriter.WriteDo(
                ioDefinition.OpenDoId,
                true,
                ioDefinition.OutputOwner);
        }
        else
        {
            // 双DO关阀：先撤销打开输出。
            outputWriter.WriteDo(
                ioDefinition.OpenDoId,
                false,
                ioDefinition.OutputOwner);

            outputWriter.WriteDo(
                ioDefinition.CloseDoId,
                true,
                ioDefinition.OutputOwner);
        }
    }

    private PositionFeedback ReadPositionFeedback(
    OnOffValvePosition commandedPosition)
    {
        var hasOpenDi =
            ioDefinition.OpenDiId is not null;

        var hasClosedDi =
            ioDefinition.ClosedDiId is not null;

        if (!hasOpenDi && !hasClosedDi)
        {
            return new PositionFeedback(
                Position: ioDefinition
                    .UseCommandAsFeedback
                        ? commandedPosition
                        : null,
                IsConflict: false);
        }

        var open =
            hasOpenDi &&
            inputReader.ReadDi(
                ioDefinition.OpenDiId!);

        var closed =
            hasClosedDi &&
            inputReader.ReadDi(
                ioDefinition.ClosedDiId!);

        if (hasOpenDi && hasClosedDi)
        {
            if (open && closed)
            {
                return new PositionFeedback(
                    Position: null,
                    IsConflict: true);
            }

            if (open)
            {
                return new PositionFeedback(
                    OnOffValvePosition.Open,
                    IsConflict: false);
            }

            if (closed)
            {
                return new PositionFeedback(
                    OnOffValvePosition.Closed,
                    IsConflict: false);
            }

            // 两个到位信号都没有，通常表示正在运动。
            return new PositionFeedback(
                Position: null,
                IsConflict: false);
        }

        if (hasOpenDi && open)
        {
            return new PositionFeedback(
                OnOffValvePosition.Open,
                IsConflict: false);
        }

        if (hasClosedDi && closed)
        {
            return new PositionFeedback(
                OnOffValvePosition.Closed,
                IsConflict: false);
        }

        // 只有一个传感器且没有触发时，
        // 不能反推出阀门一定处于另一端。
        return new PositionFeedback(
            Position: null,
            IsConflict: false);
    }

    private readonly record struct PositionFeedback(
        OnOffValvePosition? Position,
        bool IsConflict);

    private void ValidateOutput(
        string pointId,
        bool expectedSafeState)
    {
        var point =
            pointProvider.GetRequired(pointId);

        if (point.Direction !=
            DigitalIoDirection.Output)
        {
            throw new InvalidOperationException(
                $"Valve {Id} point '{pointId}' " +
                "must be a digital output.");
        }

        if (point.ProcessSafeState !=
            expectedSafeState)
        {
            throw new InvalidOperationException(
                $"Valve {Id} output '{pointId}' " +
                $"must define process safe state " +
                $"'{expectedSafeState}'.");
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
            pointProvider.GetRequired(pointId);

        if (point.Direction !=
            DigitalIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"Valve {Id} point '{pointId}' " +
                "must be a digital input.");
        }
    }

    private bool ReadOptional(
        string? pointId,
        bool defaultValue = false)
    {
        return pointId is null
            ? defaultValue
            : inputReader.ReadDi(pointId);
    }
}