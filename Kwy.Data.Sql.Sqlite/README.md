# Kwy.Data.Sql.Sqlite

`Kwy.Data.Sql.Sqlite` 是 SQLite 的原生 SQL 功能入口包。

```csharp
services.AddKwySqlite("Data Source=app.db");
```

使用：

```csharp
var sql = serviceProvider.GetRequiredService<ISqlExecutor>();

await sql.ExecuteAsync(SqlCommandDefinition.Text(
    "create table if not exists Users(Id integer primary key, Name text)"));
```
