# Kwy.Data 数据库模块设计

## 1. 模块定位

`Kwy.Data.Abstractions` 定义 Kwy 数据库模块的公共契约。它不绑定 EF Core、Dapper、ADO.NET 的某个实现，也不绑定 SQLite、SQL Server、PostgreSQL 等具体数据库。

数据库模块只解决通用的数据访问问题：

- 描述一个数据源，并按名称选择数据源。
- 创建和打开数据库连接。
- 开启、提交和回滚事务。
- 执行参数化 SQL 并显式映射查询结果。
- 在 EF Core 与原生 SQL 之间共享连接和事务。

它不定义工单、设备、工位、历史曲线、配方等业务模型。这些模型和 Repository 接口应放在对应业务模块中，由基础设施层通过 `Kwy.Data.*` 实现持久化。

## 2. 分层和依赖方向

```text
业务模块
  业务模型、Repository 接口、查询条件
                    │
                    ▼
业务基础设施
  SQL、表映射、Repository 实现、数据库迁移
          │                         │
          ▼                         ▼
Kwy.Data.Sql.<Provider>       Kwy.Data.EFCore.<Provider>
          │                         │
          ▼                         ▼
    Kwy.Data.Sql                 Kwy.Data.EFCore
          │                         │
          ▼                         │
    Kwy.Data.Core                    │
          │                         │
          └──────────┬──────────────┘
                     ▼
            Kwy.Data.Abstractions
```

箭头表示代码依赖方向。Provider 实现依赖通用层，通用抽象不依赖具体 Provider。

| 项目 | 职责 |
| --- | --- |
| `Kwy.Data.Abstractions` | 数据源、连接、事务、SQL 执行和分页的公共契约 |
| `Kwy.Data.Core` | 连接工厂基类、命名数据源解析、事务生命周期 |
| `Kwy.Data.Sql` | 基于 `DbCommand` 的通用 SQL 执行器，不引入 ORM |
| `Kwy.Data.EFCore` | 从 `DbContext` 创建 SQL 执行器，共享 EF Core 的连接和当前事务 |
| `Kwy.Data.Sql.<Provider>` | 具体 ADO.NET Provider 的连接工厂和 DI 注册 |
| `Kwy.Data.EFCore.<Provider>` | 具体 EF Core Provider 的 `UseXxx` 注册 |

## 3. 数据源

`KwyDataSourceOptions` 描述一个逻辑数据源：

```csharp
var options = new KwyDataSourceOptions
{
    Name = "History",
    Provider = KwyDatabaseProvider.PostgreSql,
    ConnectionString = connectionString,
    CommandTimeoutSeconds = 30
};

options.ValidateAndThrow();
```

| 属性 | 含义 |
| --- | --- |
| `Name` | 数据源的逻辑名称，解析时大小写不敏感 |
| `Provider` | 数据库类型，用于能力识别和诊断 |
| `ConnectionString` | Provider 使用的连接字符串 |
| `CommandTimeoutSeconds` | 数据源的默认 SQL 超时；`0` 表示由 Provider 按无限超时处理 |

`KwyDatabaseProvider` 只是数据库类型标识。枚举中存在某个值，不代表已经存在对应的 Provider 实现包。

## 4. 连接工厂

`IDatabaseConnectionFactory` 代表一个数据源的连接创建能力：

```csharp
await using DbConnection connection =
    await connectionFactory.OpenConnectionAsync(cancellationToken);
```

- `CreateConnection()` 返回未打开的连接。
- `OpenConnectionAsync()` 创建并打开连接。
- 连接所有权归调用方，调用方必须释放。
- 打开失败时，基础实现会释放已创建的连接。

业务代码通常不应直接操作连接工厂，应优先使用 `ISqlExecutor`、EF Core 或业务 Repository。

## 5. `IDatabaseConnectionFactoryResolver`

应用可以同时连接多个数据库：

```text
Default  -> SQLite，本地运行数据
History  -> PostgreSQL，历史数据
Mes      -> SQL Server，MES 数据
```

`IDatabaseConnectionFactoryResolver` 是连接工厂的路由器：

```csharp
IDatabaseConnectionFactory historyFactory =
    resolver.GetRequired("History");
```

Resolver 不创建数据库对象，它只根据名称返回已注册的连接工厂。数据源不存在或名称重复时会快速失败，避免静默连接到错误的数据库。

只注册一个数据源时，默认名称可以自动解析到该数据源。存在多个数据源时，应明确注册和使用 `Default`、`History`、`Mes` 等名称。

普通业务 Repository 应通过 `ISqlExecutorFactory` 选择数据源，Resolver 主要供事务工厂、SQL 执行器工厂和 Provider 等基础设施使用。

## 6. SQL 执行器

`ISqlExecutor` 提供四种最小能力：

```csharp
Task<int> ExecuteAsync(...);
Task<T?> ExecuteScalarAsync<T>(...);
Task<IReadOnlyList<T>> QueryAsync<T>(...);
Task<T?> QuerySingleOrDefaultAsync<T>(...);
```

它不进行实体跟踪，不生成 SQL，也不假装是 ORM。查询结果使用显式委托映射：

```csharp
IReadOnlyList<User> users = await sql.QueryAsync(
    SqlCommandDefinition.Text(
        "select Id, Name from Users where Enabled = @enabled",
        new SqlParameterValue("@enabled", true, DbType.Boolean)),
    reader => new User(reader.GetInt64(0), reader.GetString(1)),
    cancellationToken);
```

`QuerySingleOrDefaultAsync` 的语义：

- 0 行：返回 `default`。
- 1 行：返回映射结果。
- 多于 1 行：抛出 `InvalidOperationException`。

`ISqlExecutorFactory` 用于选择数据源或绑定事务：

```csharp
ISqlExecutor historySql = sqlExecutorFactory.Create("History");
ISqlExecutor transactionSql = sqlExecutorFactory.Create(transaction);
```

无事务执行器每次操作自行打开并释放连接，不长期占用连接池连接。

## 7. SQL 命令、超时和参数

`SqlCommandDefinition` 描述一条 SQL 命令：

```csharp
var command = new SqlCommandDefinition(
    Sql: "update Product set Name = @name where Id = @id",
    Parameters:
    [
        new SqlParameterValue("@name", name, DbType.String, Size: 200),
        new SqlParameterValue("@id", id, DbType.Int64)
    ],
    CommandType: CommandType.Text,
    TimeoutSeconds: 10);
```

超时优先级：

```text
SqlCommandDefinition.TimeoutSeconds
    > KwyDataSourceOptions.CommandTimeoutSeconds
    > Provider 默认值
```

EF Core 桥接器在没有命令级超时时，会继承 `DbContext.Database.GetCommandTimeout()`。

`SqlParameterValue` 支持 `DbType`、`Direction`、`Size`、`Precision` 和 `Scale`。输出参数执行完成后，结果会回写到原 `SqlParameterValue.Value`：

```csharp
var output = new SqlParameterValue(
    "@newId", null, DbType.Int64, ParameterDirection.Output);

await sql.ExecuteAsync(new SqlCommandDefinition(
    "CreateProduct", [output], CommandType.StoredProcedure));

long newId = Convert.ToInt64(output.Value);
```

参数名前缀和存储过程支持取决于具体 Provider。业务值必须通过参数传入；表名、列名等不能参数化的标识符必须来自受信任白名单。

## 8. 事务设计

```csharp
await using IDatabaseTransaction transaction =
    await transactionFactory.BeginTransactionAsync(
        "History",
        IsolationLevel.ReadCommitted,
        cancellationToken);

ISqlExecutor transactionSql = sqlExecutorFactory.Create(transaction);

await transactionSql.ExecuteAsync(insertHeader, cancellationToken);
await transactionSql.ExecuteAsync(insertDetails, cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

如果离开 `await using` 作用域时没有成功提交或显式回滚，`DatabaseTransaction` 会尝试自动回滚。无论回滚是否失败，事务对象和连接都会继续被释放。

开启事务后，必须使用从该事务创建的执行器。默认执行器会打开另一个连接，其 SQL 不会加入已开启的事务：

```csharp
// 错误：defaultSql 不属于 transaction。
await defaultSql.ExecuteAsync(command);

// 正确：使用事务绑定执行器。
ISqlExecutor transactionSql = sqlExecutorFactory.Create(transaction);
await transactionSql.ExecuteAsync(command);
```

事务绑定执行器不拥有连接和事务，不会自行提交、回滚或释放它们。它的有效期不能超过对应的 `IDatabaseTransaction`，也不应被多线程并发使用。

## 9. EF Core 桥接

`Kwy.Data.EFCore` 不替代 EF Core，它只在需要原生 SQL 时，为现有 `DbContext` 创建 `ISqlExecutor`：

```csharp
await using var transaction =
    await dbContext.Database.BeginTransactionAsync(cancellationToken);

dbContext.Add(entity);
await dbContext.SaveChangesAsync(cancellationToken);

ISqlExecutor sql = bridge.CreateExecutor(dbContext);
await sql.ExecuteAsync(
    SqlCommandDefinition.Text("update Logs set Flag = 1"),
    cancellationToken);

await transaction.CommitAsync(cancellationToken);
```

EF Core 执行器使用 `DbContext.Database.GetDbConnection()`；有当前事务时会加入该事务。如果执行前连接已打开，执行后保持打开；如果连接由桥接器打开，命令和 Reader 释放后会恢复为关闭状态。

`DbContext` 不是线程安全的，由它创建的 SQL 执行器同样不能并发使用。

## 10. 分页模型

`PageRequest` 使用从 0 开始的页索引：

```csharp
var page = new PageRequest(PageIndex: 0, PageSize: 50);
page.Validate();
long offset = page.Offset;
```

`Offset` 和 `PagedResult<T>.TotalPages` 使用 `long`，避免大数据集的整数溢出。这两个类不负责生成数据库方言；`LIMIT/OFFSET`、`OFFSET/FETCH` 或键集分页由业务查询决定。对大量历史数据，建议使用基于时间和唯一键的键集分页。

## 11. 依赖注入

### 单数据源

```csharp
services.AddKwySqlite(
    "Data Source=app.db",
    configure: options => options.CommandTimeoutSeconds = 30);
```

普通 Repository 可直接注入默认执行器：

```csharp
public sealed class UserRepository(ISqlExecutor sql);
```

### 多数据源

```csharp
services.AddKwySqlite(localConnectionString, KwyDataSourceNames.Default);
services.AddKwyPostgreSql(historyConnectionString, "History");
```

业务基础设施通过执行器工厂选择数据源：

```csharp
public sealed class HistoryRepository(ISqlExecutorFactory executorFactory)
{
    private readonly ISqlExecutor sql = executorFactory.Create("History");
}
```

应在应用组合根或基础设施层选择数据源，不要让数据源名称散落在 UI 和领域代码中。

## 12. 开发新 Provider

以 PostgreSQL 原生 SQL Provider 为例，建议新建 `Kwy.Data.Sql.PostgreSql`：

1. 引用 `Kwy.Data.Abstractions`、`Kwy.Data.Core` 和 `Kwy.Data.Sql`。
2. 引入 Npgsql Provider 包。
3. 从 `DatabaseConnectionFactoryBase` 派生 `PostgreSqlConnectionFactory`。
4. `CreateConnection()` 只创建未打开的 `NpgsqlConnection`。
5. 提供 `AddKwyPostgreSql(...)` 扩展，注册独立选项和连接工厂实例。
6. 增加连接、默认超时、命名数据源、事务提交和回滚测试。

Provider 专属能力不应进入通用抽象。例如 PostgreSQL `COPY`、SQL Server `SqlBulkCopy` 和 SQLite PRAGMA 应定义在对应 Provider 或业务基础设施包中。

EF Core Provider 应新建 `Kwy.Data.EFCore.<Provider>`，只负责 `UseXxx` 和 `AddDbContextFactory` 注册，不在其中定义业务实体。

## 13. 设计约束

- 业务层不直接依赖 Npgsql、SqlClient 或 Sqlite 具体类型。
- 通用抽象不暴露某个 Provider 的特有能力。
- 不在 `Kwy.Data.*` 中定义工单、设备、历史曲线等业务模型。
- SQL 中的业务值始终参数化，不通过字符串拼接。
- 连接、事务、命令和 Reader 必须有明确的所有者并及时释放。
- 跨多条 SQL 的原子操作必须使用事务绑定执行器。
- 通用执行器不隐式开启事务，也不隐式重试非幂等 SQL。
- 重试、断线缓冲、批量写入、数据保留和分区管理属于 Provider 或业务基础设施层。

## 14. 包选择

| 需求 | 建议包 |
| --- | --- |
| SQLite 原生 SQL | `Kwy.Data.Sql.Sqlite` |
| SQLite EF Core | `Kwy.Data.EFCore.Sqlite` |
| SQL Server EF Core | `Kwy.Data.EFCore.SqlServer` |
| 自定义 Provider | 直接引用 `Kwy.Data.Abstractions` / `Core` / `Sql` / `EFCore` |

普通应用应选择可直接使用的功能包，而不是只安装 `Kwy.Data.Abstractions`。基础包通常由功能包传递引用。
