# Kwy.Data.EFCore.Sqlite

`Kwy.Data.EFCore.Sqlite` 是 SQLite 的 EFCore 功能入口包。

```csharp
services.AddKwyEfCoreSqlite<AppDbContext>("Data Source=app.db");
```

使用：

```csharp
var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync();
```

如果需要在同一个 EFCore 事务中执行 SQL：

```csharp
var bridge = serviceProvider.GetRequiredService<IEfCoreSqlBridge<AppDbContext>>();
var sql = bridge.CreateExecutor(db);
```
