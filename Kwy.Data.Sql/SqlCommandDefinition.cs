using System.Data;

namespace Kwy.Data.Sql;

public sealed record SqlCommandDefinition(
    string Sql,
    IReadOnlyList<SqlParameterValue>? Parameters = null,
    CommandType CommandType = CommandType.Text,
    int? TimeoutSeconds = null)
{
    public static SqlCommandDefinition Text(
        string sql,
        params SqlParameterValue[] parameters)
        => new(sql, parameters);
}
