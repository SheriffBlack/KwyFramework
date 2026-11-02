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
        Catalog = catalog ?? GemInterfaceCatalog.Empty;
        Data = data ?? new GemDataSnapshot();
        Commands = commands ?? new GemCommandRegistry();
        this.options = options ?? new GemRegistryOptions();
        ValidateOptions(this.options);
    }

    public GemInterfaceCatalog Catalog { get; private set; }

    public GemDataSnapshot Data { get; }

    public GemCommandRegistry Commands { get; }

    /// <summary>指示启动配置是否已校验并封闭。</summary>
    public bool IsConfigurationSealed => Commands.IsSealed;

    /// <summary>当前已应用的客户接口合同身份，用于发布审计和现场问题追溯。</summary>
    public GemInterfaceProfileDescriptor? AppliedProfile { get; private set; }

    /// <summary>应用一份 JSON 或 C# Profile。单个 Registry 只能应用一份 Profile。</summary>
    public void ApplyProfile(IGemInterfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (IsConfigurationSealed)
            throw new InvalidOperationException("The GEM registry configuration is sealed.");
        if (AppliedProfile is not null)
            throw new InvalidOperationException($"GEM interface profile '{AppliedProfile.ProfileId}' is already applied.");
        if (Catalog.Variables.Count > 0 || Catalog.Constants.Count > 0 || Catalog.Reports.Count > 0 ||
            Catalog.Events.Count > 0 || Catalog.Alarms.Count > 0 || Catalog.RemoteCommands.Count > 0)
            throw new InvalidOperationException("A GEM interface catalog was supplied and cannot be replaced by a profile.");

        var builder = new GemInterfaceCatalogBuilder();
        profile.ApplyTo(builder);
        Catalog = builder.Build();
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

    /// <summary>校验 Remote Command 定义与业务 Handler 的绑定关系，然后封闭启动配置。</summary>
    public void SealConfiguration()
    {
        GemCatalogValidationResult result = Commands.ValidateBindingsAndSeal(Catalog.RemoteCommands);
        if (!result.IsValid)
            throw new GemCatalogValidationException(result);
    }

    public void RegisterVariable(GemVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        if (!Catalog.Variables.TryGetValue(variable.Id, out GemVariableDefinition? definition))
            throw new InvalidOperationException($"VID {variable.Id} is not defined in the GEM interface catalog.");
        if (!string.Equals(definition.Name, variable.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"VID {variable.Id} is defined as '{definition.Name}', but runtime value uses '{variable.Name}'.");
        }

        Data.SetVariable(variable);
    }

    public void RegisterConstant(GemEquipmentConstant constant)
    {
        ArgumentNullException.ThrowIfNull(constant);
        if (!Catalog.Constants.TryGetValue(constant.Id, out GemEquipmentConstantDefinition? definition))
            throw new InvalidOperationException($"ECID {constant.Id} is not defined in the GEM interface catalog.");
        if (!string.Equals(definition.Name, constant.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ECID {constant.Id} is defined as '{definition.Name}', but runtime value uses '{constant.Name}'.");
        }

        Data.SetConstant(constant);
    }

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
