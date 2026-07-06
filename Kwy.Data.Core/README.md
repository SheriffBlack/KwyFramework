# Kwy.Data.Core

`Kwy.Data.Core` 提供数据库基础实现：

- `DatabaseConnectionFactoryBase`
- `DatabaseTransaction`
- `DatabaseTransactionFactory`
- `AddKwyDataCore()`

它不绑定具体数据库 provider。通常由 `Kwy.Data.Sql.Sqlite`、`Kwy.Data.EFCore.Sqlite` 等功能包自动引用。
