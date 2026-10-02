namespace Kwy.Data.Abstractions;

/// <summary>
/// 定义按逻辑名称选择数据库连接工厂的能力。
/// </summary>
/// <remarks>
/// 用于同时存在本地数据库、历史数据库和 MES 数据库等多数据源场景。
/// Resolver 只负责路由，不创建业务 Repository，也不管理连接生命周期。
/// </remarks>
public interface IDatabaseConnectionFactoryResolver
{
    /// <summary>
    /// 获取指定名称的连接工厂；数据源未注册时抛出异常。
    /// </summary>
    /// <param name="dataSourceName">数据源逻辑名称。</param>
    /// <returns>与名称对应的连接工厂。</returns>
    IDatabaseConnectionFactory GetRequired(string dataSourceName = KwyDataSourceNames.Default);
}
