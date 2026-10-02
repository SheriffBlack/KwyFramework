# Kwy.Data.Sql.PostgreSql

`Kwy.Data.Sql.PostgreSql` 是 PostgreSQL 的原生 SQL 功能入口包，基于 Npgsql 实现。

```csharp
services.AddKwyPostgreSql(
    connectionString,
    dataSourceName: "Historian",
    configure: options => options.CommandTimeoutSeconds = 30);
```

使用命名数据源：

```csharp
ISqlExecutor sql = sqlExecutorFactory.Create("Historian");
```

多条 SQL 需要原子执行时，必须从同一事务创建执行器。PostgreSQL Binary COPY、分区管理和时序保留策略属于 Provider 或业务基础设施能力。
