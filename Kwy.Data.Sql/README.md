# Kwy.Data.Sql

`Kwy.Data.Sql` 提供原生 SQL 执行能力，不绑定具体数据库厂商，也不引入 Dapper。

核心接口：

- `ISqlExecutor`
- `SqlCommandDefinition`
- `SqlParameterValue`

查询映射使用显式委托：

```csharp
var users = await sql.QueryAsync(
    SqlCommandDefinition.Text("select Id, Name from Users"),
    reader => new User(reader.GetInt32(0), reader.GetString(1)));
```

这种设计不会假装自己是 ORM，也不会和 EFCore 的实体跟踪能力冲突。

需要在显式事务中执行 SQL 时，必须从同一事务创建执行器：

```csharp
await using var transaction = await transactionFactory.BeginTransactionAsync();
var transactionSql = sqlExecutorFactory.Create(transaction);

await transactionSql.ExecuteAsync(
    SqlCommandDefinition.Text("update Logs set Flag = 1"));

await transaction.CommitAsync();
```

多数据源场景下，使用 `sqlExecutorFactory.Create("History")` 创建绑定到指定数据源的执行器。
