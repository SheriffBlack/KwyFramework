using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Visa;

/// <summary>为可选的通信工厂注册 VISA 客户端创建规则。</summary>
public static class CommunicationFactoryExtensions
{
    /// <summary>注册通用 VISA 配置和强类型 GPIB 配置到通信客户端的映射。</summary>
    public static CommunicationFactoryBuilder RegisterVisa(this CommunicationFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .RegisterCreator<VisaConfig>(config => new VisaCommunication(config))
            .RegisterCreator<GpibConfig>(config => new VisaCommunication(config));
    }
}
