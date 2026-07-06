using System.Data;

namespace Kwy.Data.Sql;

public sealed record SqlParameterValue(
    string Name,
    object? Value,
    DbType? DbType = null,
    ParameterDirection Direction = ParameterDirection.Input);
