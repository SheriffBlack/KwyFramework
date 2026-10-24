namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 当具体协议需要在运行时选择时，可根据协议配置创建通信客户端。
/// 已明确知道具体协议的应用程序应直接构造对应的客户端。
/// </summary>
public interface IProtocolClientFactory
{
    IProtocolClient CreateClient(IProtocolConfig config);

    TClient Create<TClient, TConfig>(TConfig config)
        where TClient : class, IProtocolClient
        where TConfig : IProtocolConfig;
}

/// <summary>
/// 为受支持的协议配置类型创建一个通信客户端。
/// 由于创建器通常会被共享，其实现应保持无状态或确保线程安全。
/// </summary>
public interface IProtocolClientCreator
{
    Type ConfigType { get; }

    IProtocolClient Create(IProtocolConfig config);
}

/// <summary>针对单一协议配置类型的强类型客户端创建器约定。</summary>
public interface IProtocolClientCreator<in TConfig> : IProtocolClientCreator
    where TConfig : class, IProtocolConfig
{
    IProtocolClient Create(TConfig config);

    Type IProtocolClientCreator.ConfigType => typeof(TConfig);

    IProtocolClient IProtocolClientCreator.Create(IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Create(config as TConfig
            ?? throw new ArgumentException(
                $"Expected configuration type '{typeof(TConfig).FullName}', got '{config.GetType().FullName}'.",
                nameof(config)));
    }
}
