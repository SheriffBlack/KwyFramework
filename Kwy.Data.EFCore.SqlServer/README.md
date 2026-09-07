# Kwy.Data.EFCore.SqlServer

`Kwy.Data.EFCore.SqlServer` 是 SQL Server 的 EF Core 功能入口包。

```csharp
services.AddKwyEfCoreSqlServer<AppDbContext>(
    "Server=localhost;Database=KwyApp;Trusted_Connection=True;TrustServerCertificate=True");
```

可以通过可选回调继续配置 EF Core：

```csharp
services.AddKwyEfCoreSqlServer<AppDbContext>(
    connectionString,
    options => options.EnableDetailedErrors());
```

使用：

```csharp
var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync();
```

如果需要在同一个 EF Core 事务中执行 SQL：

```csharp
var bridge = serviceProvider.GetRequiredService<IEfCoreSqlBridge<AppDbContext>>();
var sql = bridge.CreateExecutor(db);
```
