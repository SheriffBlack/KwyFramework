using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

/// <summary>
/// 从容器中已注册的连接工厂集合按名称进行解析。
/// </summary>
/// <remarks>
/// 名称不区分大小写。只有一个数据源时，对 Default 的请求会回退到该工厂，
/// 以保持单数据源业务模块的简单注入体验。
/// </remarks>
public sealed class DatabaseConnectionFactoryResolver : IDatabaseConnectionFactoryResolver
{
    private readonly IReadOnlyDictionary<string, IDatabaseConnectionFactory> factories;

    /// <summary>构建数据源名称到连接工厂的只读索引。</summary>
    /// <param name="factories">应用已注册的全部连接工厂。</param>
    public DatabaseConnectionFactoryResolver(IEnumerable<IDatabaseConnectionFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        this.factories = factories.ToDictionary(static factory => factory.DataSourceName, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IDatabaseConnectionFactory GetRequired(string dataSourceName = KwyDataSourceNames.Default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceName);
        if (factories.TryGetValue(dataSourceName, out IDatabaseConnectionFactory? factory))
        {
            return factory;
        }

        if (factories.Count == 1 && string.Equals(dataSourceName, KwyDataSourceNames.Default, StringComparison.OrdinalIgnoreCase))
        {
            return factories.Values.Single();
        }

        throw new InvalidOperationException($"Database data source '{dataSourceName}' is not registered.");
    }
}
