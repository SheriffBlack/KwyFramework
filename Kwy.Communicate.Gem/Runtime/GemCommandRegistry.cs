using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem;

/*
1. 创建设备 GEM 接口目录
2. 注册 START Definition
3. 注册 START Handler
4. ValidateAndSeal()
5. 建立 HSMS/GEM 通信
6. 收到 S2F41 START
7. 根据 Definition 校验
8. 调用 Handler 执行 
*/

/*
new GemRemoteCommandDefinition(...) 是创建命令合同；
RegisterRemoteCommand(definition) 才是注册命令定义；
Commands.Register(...) 则是注册命令的实际处理器。 
*/

/// <summary>
/// 注册 Host 可以调用的 Remote Command
/// </summary>
/// <param name="command"></param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
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

    internal void Seal()
    {
        lock (syncRoot)
        {
            IsSealed = true;
        }
    }
}
