namespace Kwy.Data.Abstractions;

/// <summary>
/// 描述一个可注册的逻辑数据源。
/// </summary>
/// <remarks>
/// 配置属于基础设施，不应包含工单、设备等业务数据。
/// 连接字符串中的密码应由安全配置源提供，不应硬编码在业务代码中。
/// </remarks>
public sealed class KwyDataSourceOptions
{
    /// <summary>获取或设置数据源逻辑名称。</summary>
    public string Name { get; set; } = KwyDataSourceNames.Default;

    /// <summary>获取或设置数据库 Provider 类型。</summary>
    public KwyDatabaseProvider Provider { get; set; } = KwyDatabaseProvider.Unknown;

    /// <summary>获取或设置 Provider 连接字符串。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置数据源级别的默认命令超时秒数。
    /// 单条 <see cref="SqlCommandDefinition"/> 可以覆盖该值。
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }

    /// <summary>验证数据源是否具备创建连接所需的基本配置。</summary>
    /// <exception cref="ArgumentException">名称或连接字符串为空。</exception>
    /// <exception cref="InvalidOperationException">未指定 Provider。</exception>
    /// <exception cref="ArgumentOutOfRangeException">超时秒数小于 0。</exception>
    public void ValidateAndThrow()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        if (Provider == KwyDatabaseProvider.Unknown)
        {
            throw new InvalidOperationException("Database provider must be specified.");
        }

        if (CommandTimeoutSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CommandTimeoutSeconds), CommandTimeoutSeconds, "Command timeout must be greater than or equal to 0.");
        }
    }
}
