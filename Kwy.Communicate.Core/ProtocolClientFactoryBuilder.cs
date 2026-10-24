using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

/// <summary>
/// 仅组合配置驱动宿主所需的协议客户端创建器，并生成不可变工厂。
/// 该构建器不依赖任何依赖注入容器，也不在组合阶段创建客户端。
/// </summary>
public sealed class ProtocolClientFactoryBuilder
{
    private readonly Dictionary<Type, IProtocolClientCreator> creators = new();
    private bool built;

    public ProtocolClientFactoryBuilder AddCreator(IProtocolClientCreator creator)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(creator);

        if (!creators.TryAdd(creator.ConfigType, creator))
        {
            throw new InvalidOperationException(
                $"A protocol client creator for configuration type '{creator.ConfigType.FullName}' is already registered.");
        }

        return this;
    }

    /// <summary>为单一协议配置类型注册基于委托的客户端创建器。</summary>
    public ProtocolClientFactoryBuilder RegisterCreator<TConfig>(
        Func<TConfig, IProtocolClient> creator)
        where TConfig : class, IProtocolConfig
    {
        ArgumentNullException.ThrowIfNull(creator);
        return AddCreator(new DelegateProtocolClientCreator<TConfig>(creator));
    }

    public ProtocolClientFactory Build()
    {
        ThrowIfBuilt();
        built = true;
        return new ProtocolClientFactory(creators.Values);
    }

    private void ThrowIfBuilt()
    {
        if (built)
        {
            throw new InvalidOperationException("The protocol client factory builder has already been built and is immutable.");
        }
    }

    private sealed class DelegateProtocolClientCreator<TConfig>(
        Func<TConfig, IProtocolClient> creator) : IProtocolClientCreator<TConfig>
        where TConfig : class, IProtocolConfig
    {
        public IProtocolClient Create(TConfig config) => creator(config);
    }
}
