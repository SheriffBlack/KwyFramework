using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;

namespace KwyPecvd.Process.Chambers;

/// <summary>封装腔体气路的配置、MFC 绑定、指令预校验和快照生成。</summary>
internal sealed class ChamberGasSystem
{
    private readonly string chamberId;
    private readonly IHardwareComponentRegistry registry;
    private readonly IReadOnlyDictionary<
        string,
        ChamberGasLineDefinition> gasLines;

    public ChamberGasSystem(
        string chamberId,
        IEnumerable<ChamberGasLineDefinition> gasLines,
        IHardwareComponentRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chamberId);
        ArgumentNullException.ThrowIfNull(gasLines);
        ArgumentNullException.ThrowIfNull(registry);

        this.chamberId = chamberId;
        this.registry = registry;
        this.gasLines = gasLines.ToDictionary(
            gasLine => gasLine.GasName,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ChamberGasFlowSnapshot>
        CreateSnapshots() =>
        gasLines.Values
            .Select(CreateSnapshot)
            .ToArray();

    public bool TryValidateReady(
        out CommandResult failure)
    {
        foreach (var gasLine in gasLines.Values)
        {
            var mfc = GetRequiredMfc(gasLine);
            var snapshot = mfc.Snapshot;

            if (snapshot.IsOffline ||
                !snapshot.IsFeedbackValid)
            {
                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"MFC '{mfc.Id}' is not ready. " +
                    $"Diagnostic: " +
                    $"{snapshot.DiagnosticCode ?? "UNKNOWN"}.");
                return false;
            }
        }

        failure = CommandResult.Success();
        return true;
    }

    public bool TryResolveMfc(
        string gasName,
        out IMfc mfc,
        out CommandResult failure)
    {
        if (string.IsNullOrWhiteSpace(gasName))
        {
            mfc = default!;
            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "Gas name is required.");
            return false;
        }

        if (!gasLines.TryGetValue(gasName, out var gasLine))
        {
            mfc = default!;
            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                $"Gas '{gasName}' is not configured " +
                $"for chamber '{chamberId}'.");
            return false;
        }

        if (!registry.TryGet<IMfc>(gasLine.MfcId, out mfc))
        {
            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                $"MFC '{gasLine.MfcId}' for gas " +
                $"'{gasLine.GasName}' is not available.");
            return false;
        }

        ValidateBinding(gasLine, mfc);
        failure = CommandResult.Success();
        return true;
    }

    public bool TryCreatePreparationPlan(
        IReadOnlyCollection<ChamberGasTarget> targets,
        out ChamberGasPreparationPlan plan,
        out CommandResult failure)
    {
        if (targets.Count == 0)
        {
            plan = default!;
            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "At least one gas target is required.");
            return false;
        }

        var targetMap =
            new Dictionary<string, ChamberGasTarget>(
                StringComparer.OrdinalIgnoreCase);
        var commands =
            new List<ChamberGasPreparationCommand>(
                targets.Count);

        foreach (var target in targets)
        {
            if (!TryResolveMfc(
                    target.GasName,
                    out var mfc,
                    out failure))
            {
                plan = default!;
                return false;
            }

            if (!targetMap.TryAdd(target.GasName, target))
            {
                plan = default!;
                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Gas '{target.GasName}' was " +
                    "specified more than once.");
                return false;
            }

            if (!TryValidateTarget(mfc, target, out failure))
            {
                plan = default!;
                return false;
            }

            commands.Add(
                new ChamberGasPreparationCommand(
                    mfc,
                    target));
        }

        plan = new ChamberGasPreparationPlan(
            targetMap,
            commands);
        failure = CommandResult.Success();
        return true;
    }

    public GasPreparationEvaluation EvaluatePreparation(IReadOnlyDictionary<string, ChamberGasTarget> targets)
    {
        foreach (var target in targets.Values)
        {
            if (!TryResolveMfc(
                    target.GasName,
                    out var mfc,
                    out var failure))
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Faulted,
                    "GAS_MFC_NOT_AVAILABLE",
                    failure.Message);
            }

            var snapshot = mfc.Snapshot;

            if (snapshot.IsOffline)
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Faulted,
                    "GAS_MFC_OFFLINE",
                    $"MFC '{mfc.Id}' is offline.");
            }

            if (!snapshot.IsFeedbackValid)
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Faulted,
                    "GAS_FEEDBACK_INVALID",
                    $"MFC '{mfc.Id}' feedback is invalid. " +
                    $"Diagnostic: " +
                    $"{snapshot.DiagnosticCode ?? "UNKNOWN"}.");
            }

            if (snapshot.IsOutOfTolerance)
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Faulted,
                    "GAS_OUT_OF_TOLERANCE",
                    $"MFC '{mfc.Id}' remained outside " +
                    "the configured tolerance.");
            }

            if (snapshot.IsRamping)
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Waiting);
            }

            var withinTolerance =
                Math.Abs(
                    snapshot.Feedback -
                    target.Target) <=
                mfc.Definition.Tolerance;

            if (!withinTolerance)
            {
                return new GasPreparationEvaluation(
                    GasPreparationStatus.Waiting);
            }
        }

        return new GasPreparationEvaluation(
            GasPreparationStatus.Ready);
    }

    private IMfc GetRequiredMfc(
        ChamberGasLineDefinition gasLine)
    {
        var mfc = registry.GetRequired<IMfc>(gasLine.MfcId);
        ValidateBinding(gasLine, mfc);
        return mfc;
    }

    private ChamberGasFlowSnapshot CreateSnapshot(
        ChamberGasLineDefinition gasLine)
    {
        var mfc = GetRequiredMfc(gasLine);

        return new ChamberGasFlowSnapshot
        {
            GasName = gasLine.GasName,
            Mfc = mfc.Snapshot
        };
    }

    private void ValidateBinding(
        ChamberGasLineDefinition gasLine,
        IMfc mfc)
    {
        if (!string.Equals(
                mfc.Definition.ModuleId,
                chamberId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"MFC '{mfc.Id}' belongs to module " +
                $"'{mfc.Definition.ModuleId}', not " +
                $"chamber '{chamberId}'.");
        }

        if (!string.Equals(
                mfc.Definition.GasName,
                gasLine.GasName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"MFC '{mfc.Id}' is configured for gas " +
                $"'{mfc.Definition.GasName}', but chamber " +
                $"'{chamberId}' maps it to " +
                $"'{gasLine.GasName}'.");
        }
    }

    private static bool TryValidateTarget(
        IMfc mfc,
        ChamberGasTarget target,
        out CommandResult failure)
    {
        if (!double.IsFinite(target.Target))
        {
            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "MFC target must be a finite number.");
            return false;
        }

        if (target.Target < 0 ||
            target.Target > mfc.Definition.FullScale)
        {
            failure = CommandResult.Failure(
                ResultCodes.OutOfRange,
                $"MFC {mfc.Id} target must be between 0 " +
                $"and {mfc.Definition.FullScale} " +
                $"{mfc.Definition.Unit}.");
            return false;
        }

        if (target.RampDuration < TimeSpan.Zero)
        {
            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "Ramp duration cannot be negative.");
            return false;
        }

        failure = CommandResult.Success();
        return true;
    }
}

internal enum GasPreparationStatus
{
    Waiting,
    Ready,
    Faulted
}

internal sealed record GasPreparationEvaluation(
    GasPreparationStatus Status,
    string? FaultCode = null,
    string? FaultMessage = null);

internal sealed record ChamberGasPreparationPlan(
    IReadOnlyDictionary<string, ChamberGasTarget> Targets,
    IReadOnlyList<ChamberGasPreparationCommand> Commands);

internal sealed record ChamberGasPreparationCommand(
    IMfc Mfc,
    ChamberGasTarget Target);
