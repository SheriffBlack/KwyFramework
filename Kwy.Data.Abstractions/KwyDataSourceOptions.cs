namespace Kwy.Data.Abstractions;

public sealed class KwyDataSourceOptions
{
    public string Name { get; set; } = KwyDataSourceNames.Default;

    public KwyDatabaseProvider Provider { get; set; } = KwyDatabaseProvider.Unknown;

    public string ConnectionString { get; set; } = string.Empty;

    public int? CommandTimeoutSeconds { get; set; }

    public void ValidateAndThrow()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        if (Provider == KwyDatabaseProvider.Unknown)
        {
            throw new InvalidOperationException("Database provider must be specified.");
        }
    }
}
