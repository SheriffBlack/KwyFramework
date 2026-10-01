using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 组合当前 GEM 会话使用的静态接口目录、运行时快照和命令注册表。
/// </summary>
/// <remarks>
/// <see cref="Catalog"/> 描述设备支持什么；<see cref="Data"/> 保存报文构造所需的当前值；
/// <see cref="Commands"/> 描述 Host 可以调用什么。本类型不是数据库，也不是设备业务状态源。
/// </remarks>
public sealed class GemRegistry
{
    private readonly object historyLock = new();
    private readonly Queue<GemAlarmHistoryItem> alarmHistory = new();
    private readonly ConcurrentDictionary<uint, GemTraceDefinition> traces = new();
    private readonly Queue<GemTraceSample> traceSamples = new();
    private readonly Queue<GemProcessProgramChangeRecord> processProgramHistory = new();
    private readonly GemRegistryOptions options;

    public GemRegistry(
        GemInterfaceCatalog? catalog = null,
        GemDataSnapshot? data = null,
        GemCommandRegistry? commands = null,
        GemRegistryOptions? options = null)
    {
        Catalog = catalog ?? new GemInterfaceCatalog();
        Data = data ?? new GemDataSnapshot();
        Commands = commands ?? new GemCommandRegistry();
        this.options = options ?? new GemRegistryOptions();
        ValidateOptions(this.options);
    }

    public GemInterfaceCatalog Catalog { get; }

    public GemDataSnapshot Data { get; }

    public GemCommandRegistry Commands { get; }

    /// <summary>当前已应用的客户接口合同身份，用于发布审计和现场问题追溯。</summary>
    public GemInterfaceProfileDescriptor? AppliedProfile { get; private set; }

    /// <summary>应用一份 JSON 或 C# Profile。单个 Registry 只能应用一份 Profile。</summary>
    public void ApplyProfile(IGemInterfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (Catalog.IsSealed)
            throw new InvalidOperationException("The GEM interface catalog is already sealed.");
        if (AppliedProfile is not null)
            throw new InvalidOperationException($"GEM interface profile '{AppliedProfile.ProfileId}' is already applied.");

        profile.ApplyTo(Catalog);
        AppliedProfile = profile.Descriptor;
    }

    /// <summary>生成用于测试、审核和现场追溯的发布清单。</summary>
    public GemInterfaceReleaseManifest CreateReleaseManifest(string softwareVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(softwareVersion);
        if (AppliedProfile is null)
            throw new InvalidOperationException("Apply a GEM interface profile before creating a release manifest.");

        return new GemInterfaceReleaseManifest(softwareVersion, AppliedProfile, DateTimeOffset.UtcNow);
    }

    // 兼容现有调用方的只读入口；新代码优先通过 Catalog/Data/Commands 表达职责。
    public IReadOnlyDictionary<uint, GemVariable> Variables => Data.Variables;

    public IReadOnlyDictionary<uint, GemEquipmentConstant> Constants => Data.Constants;

    public IReadOnlyDictionary<uint, GemAlarm> Alarms => Data.Alarms;

    public IReadOnlyDictionary<uint, GemVariableDefinition> VariableDefinitions => Catalog.Variables;

    public IReadOnlyDictionary<uint, GemEquipmentConstantDefinition> ConstantDefinitions => Catalog.Constants;

    public IReadOnlyDictionary<uint, GemReportDefinition> ReportDefinitions => Catalog.Reports;

    public IReadOnlyDictionary<uint, GemCollectionEventDefinition> EventDefinitions => Catalog.Events;

    public IReadOnlyDictionary<uint, GemAlarmDefinition> AlarmDefinitions => Catalog.Alarms;

    public IReadOnlyDictionary<uint, GemReport> Reports => Catalog.Reports.ToDictionary(
        pair => pair.Key,
        pair => new GemReport(pair.Key, pair.Value.VariableIds.Select(x => x.Value).ToArray()));

    public IReadOnlyDictionary<uint, GemCollectionEvent> Events => Catalog.Events.ToDictionary(
        pair => pair.Key,
        pair => new GemCollectionEvent(
            pair.Key,
            pair.Value.Name,
            pair.Value.LinkedReports.Select(x => x.Value).ToArray()));

    public IReadOnlyList<GemAlarmHistoryItem> AlarmHistory
    {
        get
        {
            lock (historyLock)
            {
                return alarmHistory.ToArray();
            }
        }
    }

    public IReadOnlyDictionary<uint, GemTraceDefinition> Traces => traces;

    public IReadOnlyList<GemTraceSample> TraceSamples
    {
        get
        {
            lock (historyLock)
            {
                return traceSamples.ToArray();
            }
        }
    }

    public IReadOnlyList<GemProcessProgramChangeRecord> ProcessProgramHistory
    {
        get
        {
            lock (historyLock)
            {
                return processProgramHistory.ToArray();
            }
        }
    }

    public void ValidateAndSeal()
    {
        GemCatalogValidationResult catalogResult = Catalog.Validate();
        var issues = catalogResult.Issues.ToList();

        foreach (GemRemoteCommandDefinition command in Catalog.RemoteCommands.Values)
        {
            if (!Commands.CommandNames.Contains(command.EffectiveHandlerKey, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new GemCatalogValidationIssue(
                    "GEM010",
                    $"Remote command '{command.Name}' requires handler '{command.EffectiveHandlerKey}', but it is not registered."));
            }
        }

        HashSet<string> requiredHandlerKeys = Catalog.RemoteCommands.Values
            .Select(command => command.EffectiveHandlerKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string handlerKey in Commands.CommandNames)
        {
            if (!requiredHandlerKeys.Contains(handlerKey))
            {
                issues.Add(new GemCatalogValidationIssue(
                    "GEM011",
                    $"Remote command handler '{handlerKey}' is not referenced by any interface definition."));
            }
        }

        if (issues.Count > 0)
        {
            throw new GemCatalogValidationException(new GemCatalogValidationResult(issues));
        }

        Catalog.ValidateAndSeal();
        Commands.Seal();
    }

    public void RegisterVariable(GemVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        if (!Catalog.Variables.TryGetValue(variable.Id, out GemVariableDefinition? definition))
        {
            Catalog.RegisterVariable(new GemVariableDefinition(
                new GemVid(variable.Id),
                variable.Name,
                GemVariableKind.DataVariable,
                variable.Unit));
        }
        else if (!string.Equals(definition.Name, variable.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"VID {variable.Id} is defined as '{definition.Name}', but runtime value uses '{variable.Name}'.");
        }

        Data.SetVariable(variable);
    }

    public void RegisterVariableDefinition(GemVariableDefinition variable) => Catalog.RegisterVariable(variable);

    public void RegisterConstant(GemEquipmentConstant constant)
    {
        ArgumentNullException.ThrowIfNull(constant);
        if (!Catalog.Constants.TryGetValue(constant.Id, out GemEquipmentConstantDefinition? definition))
        {
            Catalog.RegisterConstant(new GemEquipmentConstantDefinition(
                new GemEcid(constant.Id),
                constant.Name,
                constant.Unit));
        }
        else if (!string.Equals(definition.Name, constant.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ECID {constant.Id} is defined as '{definition.Name}', but runtime value uses '{constant.Name}'.");
        }

        Data.SetConstant(constant);
    }

    public void RegisterConstantDefinition(GemEquipmentConstantDefinition constant) => Catalog.RegisterConstant(constant);

    public void RegisterReport(GemReport report)
        => Catalog.RegisterReport(new GemReportDefinition(
            new GemRptid(report.ReportId),
            report.VariableIds.Select(x => new GemVid(x)).ToArray()));

    public void RegisterReportDefinition(GemReportDefinition report) => Catalog.RegisterReport(report);

    public void RegisterEvent(GemCollectionEvent collectionEvent)
        => Catalog.RegisterEvent(new GemCollectionEventDefinition(
            new GemCeid(collectionEvent.EventId),
            collectionEvent.Name,
            collectionEvent.LinkedReportIds.Select(x => new GemRptid(x)).ToArray()));

    public void RegisterEventDefinition(GemCollectionEventDefinition collectionEvent) => Catalog.RegisterEvent(collectionEvent);

    public void RegisterAlarmDefinition(GemAlarmDefinition alarm) => Catalog.RegisterAlarm(alarm);

    public void SetAlarm(GemAlarm alarm)
    {
        Data.SetAlarm(alarm);
        lock (historyLock)
        {
            EnqueueBounded(
                alarmHistory,
                new GemAlarmHistoryItem(alarm, DateTimeOffset.Now),
                options.MaximumAlarmHistory);
        }
    }

    public void RegisterCommand(string commandName, GemRemoteCommandHandler handler)
        => Commands.Register(commandName, handler);

    public bool TryGetCommand(string commandName, out GemRemoteCommandHandler handler)
        => Commands.TryGet(commandName, out handler);

    public void RecordProcessProgramChange(
        string ppid,
        string action,
        string actor = "System",
        string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        lock (historyLock)
        {
            EnqueueBounded(
                processProgramHistory,
                new GemProcessProgramChangeRecord(ppid, action, actor, DateTimeOffset.Now, reason),
                options.MaximumProcessProgramHistory);
        }
    }

    public void RegisterTrace(GemTraceDefinition trace) => traces[trace.TraceId] = trace;

    public void AddTraceSample(GemTraceSample sample)
    {
        lock (historyLock)
        {
            EnqueueBounded(traceSamples, sample, options.MaximumTraceSamples);
        }
    }

    private static void EnqueueBounded<T>(Queue<T> queue, T item, int maximumCount)
    {
        queue.Enqueue(item);
        while (queue.Count > maximumCount)
        {
            queue.Dequeue();
        }
    }

    private static void ValidateOptions(GemRegistryOptions options)
    {
        if (options.MaximumAlarmHistory <= 0 ||
            options.MaximumProcessProgramHistory <= 0 ||
            options.MaximumTraceSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "GEM registry history limits must be greater than zero.");
        }
    }
}
