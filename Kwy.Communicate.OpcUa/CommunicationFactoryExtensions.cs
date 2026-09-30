using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Opc.Ua.Client;

namespace Kwy.Communicate.OpcUa;

/// <summary>OPC UA 通信工厂注册扩展。</summary>
public static class CommunicationFactoryExtensions
{
    /// <summary>使用默认 OPC Foundation Session Factory 注册 OPC UA 客户端。</summary>
    public static CommunicationFactoryBuilder RegisterOpcUa(this CommunicationFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<OpcUaConfig>(config => new OpcUaCommunication(config));
    }

    /// <summary>使用指定 Session Factory 注册 OPC UA 客户端。</summary>
    public static CommunicationFactoryBuilder RegisterOpcUa(
        this CommunicationFactoryBuilder builder,
        ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        return builder.RegisterCreator<OpcUaConfig>(config => new OpcUaCommunication(config, sessionFactory));
    }
}
