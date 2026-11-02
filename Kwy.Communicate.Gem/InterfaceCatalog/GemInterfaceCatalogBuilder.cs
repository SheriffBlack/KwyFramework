namespace Kwy.Communicate.Gem;

/// <summary>在启动阶段注册、校验并构建 GEM 接口目录。</summary>
public sealed class GemInterfaceCatalogBuilder
{
    private readonly Dictionary<uint, GemVariableDefinition> variables = new();
    private readonly Dictionary<uint, GemEquipmentConstantDefinition> constants = new();
    private readonly Dictionary<uint, GemReportDefinition> reports = new();
    private readonly Dictionary<uint, GemCollectionEventDefinition> events = new();
    private readonly Dictionary<uint, GemAlarmDefinition> alarms = new();
    private readonly Dictionary<string, GemRemoteCommandDefinition> remoteCommands = new(StringComparer.OrdinalIgnoreCase);

    public GemInterfaceCatalogBuilder AddVariable(GemVariableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(variables, definition.Vid.Value, definition with { }, definition.Name, "VID");
        return this;
    }

    public GemInterfaceCatalogBuilder AddConstant(GemEquipmentConstantDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(constants, definition.Ecid.Value, definition with { }, definition.Name, "ECID");
        return this;
    }

    public GemInterfaceCatalogBuilder AddReport(GemReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(reports, definition.Rptid.Value,
            definition with { VariableIds = definition.VariableIds.ToArray() },
            $"RPTID_{definition.Rptid.Value}", "RPTID", false);
        return this;
    }

    public GemInterfaceCatalogBuilder AddEvent(GemCollectionEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(events, definition.Ceid.Value,
            definition with { LinkedReports = definition.LinkedReports.ToArray() },
            definition.Name, "CEID");
        return this;
    }

    public GemInterfaceCatalogBuilder AddAlarm(GemAlarmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Add(alarms, definition.Alid.Value, definition with { }, definition.Code, "ALID");
        return this;
    }

    public GemInterfaceCatalogBuilder AddRemoteCommand(GemRemoteCommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        if (remoteCommands.ContainsKey(definition.Name))
            throw new InvalidOperationException($"Remote command '{definition.Name}' is already defined.");
        remoteCommands.Add(definition.Name, definition with
        {
            Parameters = definition.Parameters.ToArray(),
            AllowedControlStates = definition.AllowedControlStates is null
                ? null
                : new HashSet<GemControlState>(definition.AllowedControlStates)
        });
        return this;
    }

    public GemCatalogValidationResult Validate()
    {
        var issues = new List<GemCatalogValidationIssue>();
        foreach (GemReportDefinition report in reports.Values)
        {
            if (report.VariableIds.Count == 0)
                issues.Add(new("GEM002", $"RPTID {report.Rptid.Value} does not contain any VID."));
            AddDuplicateIssues(report.VariableIds, x => x.Value, "GEM005",
                id => $"RPTID {report.Rptid.Value} contains duplicate VID {id}.", issues);
            foreach (GemVid vid in report.VariableIds.Where(vid => !variables.ContainsKey(vid.Value)))
                issues.Add(new("GEM003", $"RPTID {report.Rptid.Value} references undefined VID {vid.Value}."));
        }

        foreach (GemCollectionEventDefinition item in events.Values)
        {
            AddDuplicateIssues(item.LinkedReports, x => x.Value, "GEM006",
                id => $"CEID {item.Ceid.Value} contains duplicate RPTID {id}.", issues);
            foreach (GemRptid rptid in item.LinkedReports.Where(rptid => !reports.ContainsKey(rptid.Value)))
                issues.Add(new("GEM004", $"CEID {item.Ceid.Value} references undefined RPTID {rptid.Value}."));
        }

        foreach (GemRemoteCommandDefinition command in remoteCommands.Values)
        {
            if (string.IsNullOrWhiteSpace(command.EffectiveHandlerKey))
                issues.Add(new("GEM012", $"Remote command '{command.Name}' has an empty HandlerKey."));
            if (command.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Name)))
                issues.Add(new("GEM007", $"Remote command '{command.Name}' contains an empty parameter name."));
            foreach (IGrouping<string, GemRemoteCommandParameterDefinition> duplicate in command.Parameters
                         .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
                issues.Add(new("GEM008", $"Remote command '{command.Name}' contains duplicate parameter '{duplicate.Key}'."));
            ValidateCommandEvent(command.Name, "AcceptedEvent", command.AcceptedEvent, issues);
            ValidateCommandEvent(command.Name, "CompletedEvent", command.CompletedEvent, issues);
            ValidateCommandEvent(command.Name, "FailedEvent", command.FailedEvent, issues);
        }
        return new GemCatalogValidationResult(issues.ToArray());
    }

    public GemInterfaceCatalog Build()
    {
        GemCatalogValidationResult result = Validate();
        if (!result.IsValid)
            throw new GemCatalogValidationException(result);
        return new GemInterfaceCatalog(variables, constants, reports, events, alarms, remoteCommands);
    }

    private void ValidateCommandEvent(string commandName, string propertyName, GemCeid? eventId,
        ICollection<GemCatalogValidationIssue> issues)
    {
        if (eventId is GemCeid ceid && !events.ContainsKey(ceid.Value))
            issues.Add(new("GEM009", $"Remote command '{commandName}' {propertyName} references undefined CEID {ceid.Value}."));
    }

    private static void Add<T>(Dictionary<uint, T> target, uint id, T value, string name,
        string idName, bool checkName = true)
    {
        if (id == 0)
            throw new ArgumentOutOfRangeException(nameof(id), $"{idName} must be greater than zero.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (target.ContainsKey(id))
            throw new InvalidOperationException($"{idName} {id} is already registered.");
        if (checkName && target.Values.Any(item =>
                string.Equals(GetName(item), name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{idName} name '{name}' is already registered.");
        target.Add(id, value);
    }

    private static void AddDuplicateIssues<T>(IEnumerable<T> source, Func<T, uint> idSelector,
        string code, Func<uint, string> messageFactory, ICollection<GemCatalogValidationIssue> issues)
    {
        foreach (uint id in source.GroupBy(idSelector).Where(group => group.Count() > 1).Select(group => group.Key))
            issues.Add(new(code, messageFactory(id)));
    }

    private static string? GetName<T>(T definition) => definition switch
    {
        GemVariableDefinition variable => variable.Name,
        GemEquipmentConstantDefinition constant => constant.Name,
        GemCollectionEventDefinition collectionEvent => collectionEvent.Name,
        GemAlarmDefinition alarm => alarm.Code,
        _ => null
    };
}
