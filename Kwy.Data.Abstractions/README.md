# Kwy.Data.Abstractions

`Kwy.Data.Abstractions` 定义数据库访问的公共抽象，不绑定 EFCore、Dapper 或具体数据库厂商。

它只包含：

- 数据源配置：`KwyDataSourceOptions`
- 数据库类型：`KwyDatabaseProvider`
- 连接工厂：`IDatabaseConnectionFactory`
- 事务抽象：`IDatabaseTransaction` / `IDatabaseTransactionFactory`
- 通用分页模型：`PageRequest` / `PagedResult<T>`

推荐安装功能包，例如 `Kwy.Data.Sql.Sqlite` 或 `Kwy.Data.EFCore.Sqlite`。基础包通常由 NuGet 自动解析依赖；仅当你需要开发自定义数据库 provider、SQL 执行器或 EFCore 扩展时，才直接引用本包。
