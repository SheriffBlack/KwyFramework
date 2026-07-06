# Kwy.Data.EFCore

`Kwy.Data.EFCore` 提供 EFCore 相关基础能力，但不绑定具体数据库 provider。

核心能力：

- `IEfCoreSqlBridge<TContext>`
- `EfCoreSqlExecutor`

当 EFCore 与原生 SQL 混用时，建议通过 bridge 从当前 `DbContext` 创建 SQL executor，这样可以共享连接和当前事务。

```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync();

dbContext.Add(entity);
await dbContext.SaveChangesAsync();

var sql = bridge.CreateExecutor(dbContext);
await sql.ExecuteAsync(SqlCommandDefinition.Text("update Logs set Flag = 1"));

await transaction.CommitAsync();
```
