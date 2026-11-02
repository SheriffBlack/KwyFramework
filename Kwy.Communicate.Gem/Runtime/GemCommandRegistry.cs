using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 处理一条已通过接口合同校验的 Remote Command。
/// </summary>
/// <remarks>
/// 该委托的完成时间直接影响 S2F42 回复时间，实现必须在 Host T3 内返回。
/// 耗时设备动作应转换为应用层自有的命令快照并入队，不应长期持有收到的 SECS Item。
/// </remarks>
public delegate Task<GemRemoteCommandResult> GemRemoteCommandHandler(
    GemRemoteCommand command,
    CancellationToken cancellationToken);

/// <summary>
/// 保存 Host 可调用的远程命令处理器。
/// Fab 进场前先确认支持哪些 Remote Command；
/// 设备软件启动时加载命令定义并注册处理器；
/// 运行期间只执行已确认、已注册的命令，不应临时接受未知命令。
/// </summary>
public sealed class GemCommandRegistry
{
    private readonly object syncRoot = new();
    private readonly ConcurrentDictionary<string, GemRemoteCommandHandler> handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> CommandNames => handlers.Keys.ToArray();

    public bool IsSealed { get; private set; }

    public void Register(string commandName, GemRemoteCommandHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(handler);
        lock (syncRoot)
        {
            if (IsSealed)
            {
                throw new InvalidOperationException("The GEM command registry is sealed and cannot be modified.");
            }

            if (!handlers.TryAdd(commandName, handler))
            {
                throw new InvalidOperationException($"Remote command '{commandName}' is already registered.");
            }
        }
    }

    public bool TryGet(string commandName, out GemRemoteCommandHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return handlers.TryGetValue(commandName, out handler!);
    }

    internal GemCatalogValidationResult ValidateBindingsAndSeal(
        IReadOnlyDictionary<string, GemRemoteCommandDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        lock (syncRoot)
        {
            if (IsSealed)
                return new GemCatalogValidationResult([]);

            var issues = new List<GemCatalogValidationIssue>();
            foreach (GemRemoteCommandDefinition command in definitions.Values)
            {
                if (!handlers.ContainsKey(command.EffectiveHandlerKey))
                {
                    issues.Add(new(
                        "GEM010",
                        $"Remote command '{command.Name}' requires handler '{command.EffectiveHandlerKey}', but it is not registered."));
                }
            }

            HashSet<string> requiredHandlerKeys = definitions.Values
                .Select(command => command.EffectiveHandlerKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string handlerKey in handlers.Keys)
            {
                if (!requiredHandlerKeys.Contains(handlerKey))
                {
                    issues.Add(new(
                        "GEM011",
                        $"Remote command handler '{handlerKey}' is not referenced by any interface definition."));
                }
            }

            if (issues.Count > 0)
                return new GemCatalogValidationResult(issues);

            IsSealed = true;
            return new GemCatalogValidationResult([]);
        }
    }
}
