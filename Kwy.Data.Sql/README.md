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
