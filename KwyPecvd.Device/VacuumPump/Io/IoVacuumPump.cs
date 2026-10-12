using Kwy.Device.IoCard.Abstractions;

namespace KwyPecvd.Device.VacuumPump.Io;

public sealed class IoVacuumPump : VacuumPumpBase
{
    private readonly IoVacuumPumpDefinition ioDefinition;
    private readonly IDigitalIoPointDefinitionProvider pointProvider;
    private readonly ILogicalDigitalInputReader inputReader;
    private readonly ILogicalDigitalOutputWriter outputWriter;

    public IoVacuumPump(
        IoVacuumPumpDefinition definition,
        IDigitalIoPointDefinitionProvider pointProvider,
        ILogicalDigitalInputReader inputReader,
        ILogicalDigitalOutputWriter outputWriter)
        : base(definition.Pump)
    {
        definition.Validate();

        ioDefinition = definition;
        this.pointProvider = pointProvider;
        this.inputReader = inputReader;
        this.outputWriter = outputWriter;
    }

    protected override Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        ValidateOutput(
            ioDefinition.RunCommandDoId,
            "运行命令");

        ValidateInput(
            ioDefinition.RunningDiId,
            "运行反馈");

        ValidateOptionalInput(
            ioDefinition.WarningDiId,
            "警告反馈");

        ValidateOptionalInput(
            ioDefinition.AlarmDiId,
            "故障反馈");

        ValidateOptionalInput(
            ioDefinition.OverloadDiId,
            "过载反馈");

        ValidateOptionalInput(
            ioDefinition.N2ReadyDiId,
            "氮气就绪反馈");

        ValidateOptionalInput(
            ioDefinition.ExhaustReadyDiId,
            "排气就绪反馈");

        ValidateOptionalInput(
            ioDefinition.OfflineDiId,
            "离线反馈");

        // 初始化阶段先确保输出处于安全状态。
        outputWriter.WriteDo(
            ioDefinition.RunCommandDoId,
            false,
            ioDefinition.OutputOwner);

        return Task.CompletedTask;
    }

    protected override ValueTask<VacuumPumpCycleResult> ExchangeAsync(
            bool commandedOn,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 停止命令优先输出。
        // 即使随后读取反馈失败，泵的运行DO也已经撤销。
        if (!commandedOn)
        {
            WriteRunCommand(false);
        }

        var running =
            inputReader.ReadDi(ioDefinition.RunningDiId);

        var warning =
            ReadOptional(ioDefinition.WarningDiId);

        var alarm =
            ReadOptional(ioDefinition.AlarmDiId);

        var overload =
            ReadOptional(ioDefinition.OverloadDiId);

        // 没有配置该反馈时，表示此型号不具备该信号，
        // 而不是表示条件不满足。
        var n2Ready =
            ReadOptional(
                ioDefinition.N2ReadyDiId,
                defaultValue: true);

        var exhaustReady =
            ReadOptional(
                ioDefinition.ExhaustReadyDiId,
                defaultValue: true);

        var offline =
            ReadOptional(ioDefinition.OfflineDiId);

        if (commandedOn)
        {
            // 当前周期已经检测到严重故障时，
            // 不允许运行DO继续保持。
            var safeCommand =
                !offline &&
                !alarm &&
                !overload;

            WriteRunCommand(safeCommand);
        }

        var now = DateTimeOffset.UtcNow;

        return ValueTask.FromResult(
            new VacuumPumpCycleResult(
                IsRunning: running,
                HasWarning: warning,
                HasAlarm: alarm,
                IsOverloaded: overload,
                IsN2Ready: n2Ready,
                IsExhaustReady: exhaustReady,
                IsOffline: offline,
                IsFeedbackValid: !offline,
                Timestamp: now,
                DiagnosticCode: null));
    }

    private void WriteRunCommand(bool value)
    {
        outputWriter.WriteDo(
            ioDefinition.RunCommandDoId,
            value,
            ioDefinition.OutputOwner);
    }

    private bool ReadOptional(
        string? pointId,
        bool defaultValue = false)
    {
        return string.IsNullOrWhiteSpace(pointId)
            ? defaultValue
            : inputReader.ReadDi(pointId);
    }

    private void ValidateOutput(
        string pointId,
        string role)
    {
        var point = pointProvider.GetRequired(pointId);

        if (point.Direction != DigitalIoDirection.Output)
        {
            throw new InvalidOperationException(
                $"泵{role}点位 {pointId} 不是数字输出点。");
        }

        if (point.ProcessSafeState != false)
        {
            throw new InvalidOperationException(
                $"泵{role}点位 {pointId} 的工艺安全状态必须为 false。");
        }
    }

    private void ValidateInput(
        string pointId,
        string role)
    {
        var point = pointProvider.GetRequired(pointId);

        if (point.Direction != DigitalIoDirection.Input)
        {
            throw new InvalidOperationException(
                $"泵{role}点位 {pointId} 不是数字输入点。");
        }
    }

    private void ValidateOptionalInput(
        string? pointId,
        string role)
    {
        if (!string.IsNullOrWhiteSpace(pointId))
        {
            ValidateInput(pointId, role);
        }
    }
}