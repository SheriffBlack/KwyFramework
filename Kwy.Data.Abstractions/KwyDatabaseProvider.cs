namespace Kwy.Data.Abstractions;

/// <summary>
/// 标识数据源使用的数据库 Provider 类型。
/// </summary>
public enum KwyDatabaseProvider
{
    Unknown = 0,
    Sqlite = 1,
    SqlServer = 2,
    MySql = 3,
    Oracle = 4,
    PostgreSql = 5
}
