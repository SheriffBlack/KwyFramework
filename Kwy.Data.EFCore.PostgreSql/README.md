# Kwy.Data.EFCore.PostgreSql

`Kwy.Data.EFCore.PostgreSql` 是 PostgreSQL 的 EF Core 功能入口包，基于 `Npgsql.EntityFrameworkCore.PostgreSQL`。

```csharp
services.AddKwyEfCorePostgreSql<BusinessDbContext>(
    connectionString,
    configure: options => options.EnableDetailedErrors(),
    configurePostgreSql: options => options.EnableRetryOnFailure());
```

通过 `IDbContextFactory<BusinessDbContext>` 创建业务 `DbContext`。如需在同一 EF Core 连接或事务中执行原生 SQL，使用 `IEfCoreSqlBridge<BusinessDbContext>`。

本包只负责 Provider 注册，业务实体、映射和 Migration 应定义在业务基础设施项目中。
