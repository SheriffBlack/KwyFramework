using System.Collections.ObjectModel;

namespace Kwy.Communicate.Gem;

/*
Variables   → VID   定义   变量 ID
Constants   → ECID  定义   Equipment Constant ID，设备常量 ID
Reports     → RPTID 定义   RPTID（Report ID，报表 ID）
Events      → CEID  定义   Collection Event ID，收集事件 ID
Alarms      → ALID  定义   Alarm ID，报警 ID
*/

/// <summary>
/// 保存设备对 Host 公开的静态 GEM 接口合同
/// 定义 VID、RPTID、CEID、ALID、ECID
/// </summary>
/// <remarks>
/// 接口目录通常在启动阶段根据 SEDD 或客户接口表构建。调用
/// <see cref="ValidateAndSeal"/> 后目录不可再修改，避免设备运行期间接口漂移。
/// </remarks>
public sealed class GemInterfaceCatalog
{
    private readonly object syncRoot = new();
    private readonly Dictionary<uint, GemVariableDefinition> variables = new();
    private readonly Dictionary<uint, GemEquipmentConstantDefinition> constants = new();
    private readonly Dictionary<uint, GemReportDefinition> reports = new();
    private readonly Dictionary<uint, GemCollectionEventDefinition> events = new();
    private readonly Dictionary<uint, GemAlarmDefinition> alarms = new();
    private readonly Dictionary<string, GemRemoteCommandDefinition> remoteCommands =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsSealed { get; private set; }

    public IReadOnlyDictionary<uint, GemVariableDefinition> Variables
        => new ReadOnlyDictionary<uint, GemVariableDefinition>(variables);

    public IReadOnlyDictionary<uint, GemEquipmentConstantDefinition> Constants
        => new ReadOnlyDictionary<uint, GemEquipmentConstantDefinition>(constants);

    public IReadOnlyDictionary<uint, GemReportDefinition> Reports
        => new ReadOnlyDictionary<uint, GemReportDefinition>(reports);

    public IReadOnlyDictionary<uint, GemCollectionEventDefinition> Events
        => new ReadOnlyDictionary<uint, GemCollectionEventDefinition>(events);

    public IReadOnlyDictionary<uint, GemAlarmDefinition> Alarms
        => new ReadOnlyDictionary<uint, GemAlarmDefinition>(alarms);

    public IReadOnlyDictionary<string, GemRemoteCommandDefinition> RemoteCommands
        => new ReadOnlyDictionary<string, GemRemoteCommandDefinition>(remoteCommands);

    public void RegisterVariable(GemVariableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Register(variables, definition.Vid.Value, definition with { }, definition.Name, "VID");
    }

    public void RegisterConstant(GemEquipmentConstantDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Register(constants, definition.Ecid.Value, definition with { }, definition.Name, "ECID");
    }

    public void RegisterReport(GemReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Register(
            reports,
            definition.Rptid.Value,
            definition with { VariableIds = definition.VariableIds.ToArray() },
            $"RPTID_{definition.Rptid.Value}",
            "RPTID",
            checkName: false);
    }

    public void RegisterEvent(GemCollectionEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Register(
            events,
            definition.Ceid.Value,
            definition with { LinkedReports = definition.LinkedReports.ToArray() },
            definition.Name,
            "CEID");
    }

    public void RegisterAlarm(GemAlarmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Register(alarms, definition.Alid.Value, definition with { }, definition.Code, "ALID");
    }

    public void RegisterRemoteCommand(GemRemoteCommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);

        lock (syncRoot)
        {
            EnsureMutable();
            if (remoteCommands.ContainsKey(definition.Name))
            {
                throw new InvalidOperationException($"Remote command '{definition.Name}' is already defined.");
            }

            remoteCommands.Add(definition.Name, definition with
            {
                Parameters = definition.Parameters.ToArray(),
                AllowedControlStates = definition.AllowedControlStates is null
                    ? null
                    : new HashSet<GemControlState>(definition.AllowedControlStates)
            });
        }
    }

    /// <summary>检查接口编号、名称以及 CEID/RPTID/VID 引用关系。</summary>
    public GemCatalogValidationResult Validate()
    {
        lock (syncRoot)
        {
            var issues = new List<GemCatalogValidationIssue>();

            foreach (GemReportDefinition report in reports.Values)
            {
                if (report.VariableIds.Count == 0)
                {
                    issues.Add(new("GEM002", $"RPTID {report.Rptid.Value} does not contain any VID."));
                }

                foreach (GemVid duplicate in report.VariableIds
                             .GroupBy(x => x.Value)
                             .Where(group => group.Count() > 1)
                             .Select(group => group.First()))
                {
                    issues.Add(new(
                        "GEM005",
                        $"RPTID {report.Rptid.Value} contains duplicate VID {duplicate.Value}."));
                }

                foreach (GemVid vid in report.VariableIds)
                {
                    if (!variables.ContainsKey(vid.Value))
                    {
                        issues.Add(new("GEM003", $"RPTID {report.Rptid.Value} references undefined VID {vid.Value}."));
                    }
                }
            }

            foreach (GemCollectionEventDefinition collectionEvent in events.Values)
            {
                foreach (GemRptid duplicate in collectionEvent.LinkedReports
                             .GroupBy(x => x.Value)
                             .Where(group => group.Count() > 1)
                             .Select(group => group.First()))
                {
                    issues.Add(new(
                        "GEM006",
                        $"CEID {collectionEvent.Ceid.Value} contains duplicate RPTID {duplicate.Value}."));
                }

                foreach (GemRptid rptid in collectionEvent.LinkedReports)
                {
                    if (!reports.ContainsKey(rptid.Value))
                    {
                        issues.Add(new("GEM004", $"CEID {collectionEvent.Ceid.Value} references undefined RPTID {rptid.Value}."));
                    }
                }
            }

            foreach (GemRemoteCommandDefinition command in remoteCommands.Values)
            {
                if (string.IsNullOrWhiteSpace(command.EffectiveHandlerKey))
                {
                    issues.Add(new("GEM012", $"Remote command '{command.Name}' has an empty HandlerKey."));
                }

                if (command.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Name)))
                {
                    issues.Add(new("GEM007", $"Remote command '{command.Name}' contains an empty parameter name."));
                }

                foreach (IGrouping<string, GemRemoteCommandParameterDefinition> duplicate in command.Parameters
                             .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                             .Where(group => group.Count() > 1))
                {
                    issues.Add(new(
                        "GEM008",
                        $"Remote command '{command.Name}' contains duplicate parameter '{duplicate.Key}'."));
                }

                ValidateCommandEvent(command.Name, "AcceptedEvent", command.AcceptedEvent, issues);
                ValidateCommandEvent(command.Name, "CompletedEvent", command.CompletedEvent, issues);
                ValidateCommandEvent(command.Name, "FailedEvent", command.FailedEvent, issues);
            }

            return new GemCatalogValidationResult(issues.ToArray());
        }
    }

    private void ValidateCommandEvent(
        string commandName,
        string propertyName,
        GemCeid? eventId,
        ICollection<GemCatalogValidationIssue> issues)
    {
        if (eventId is GemCeid ceid && !events.ContainsKey(ceid.Value))
        {
            issues.Add(new(
                "GEM009",
                $"Remote command '{commandName}' {propertyName} references undefined CEID {ceid.Value}."));
        }
    }

    /// <summary>验证接口目录，并在验证通过后禁止继续修改。</summary>
    public void ValidateAndSeal()
    {
        lock (syncRoot)
        {
            if (IsSealed)
            {
                return;
            }

            GemCatalogValidationResult result = Validate();
            if (!result.IsValid)
            {
                throw new GemCatalogValidationException(result);
            }

            IsSealed = true;
        }
    }

    private void Register<T>(
        Dictionary<uint, T> target,
        uint id,
        T value,
        string name,
        string idName,
        bool checkName = true)
    {
        lock (syncRoot)
        {
            EnsureMutable();
            if (id == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), $"{idName} must be greater than zero.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (target.ContainsKey(id))
            {
                throw new InvalidOperationException($"{idName} {id} is already registered.");
            }

            if (checkName && target.Values.Any(item =>
                    string.Equals(GetName(item), name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"{idName} name '{name}' is already registered.");
            }

            target.Add(id, value);
        }
    }

    private static string? GetName<T>(T definition) => definition switch
    {
        GemVariableDefinition variable => variable.Name,
        GemEquipmentConstantDefinition constant => constant.Name,
        GemCollectionEventDefinition collectionEvent => collectionEvent.Name,
        GemAlarmDefinition alarm => alarm.Code,
        _ => null
    };

    private void EnsureMutable()
    {
        if (IsSealed)
        {
            throw new InvalidOperationException("The GEM interface catalog is sealed and cannot be modified.");
        }
    }
}

public sealed record GemCatalogValidationIssue(string Code, string Message);

public sealed record GemCatalogValidationResult(IReadOnlyList<GemCatalogValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class GemCatalogValidationException : InvalidOperationException
{
    public GemCatalogValidationException(GemCatalogValidationResult result)
        : base($"The GEM interface catalog is invalid: {string.Join("; ", result.Issues.Select(x => x.Message))}")
    {
        Result = result;
    }

    public GemCatalogValidationResult Result { get; }
}
