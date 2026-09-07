namespace KwyTemplate.MES.Abstract.Models;

public sealed record MesExchangeRecord(
    string Operation,
    int? ReturnCode = null,
    string? ReturnMessage = null,
    string? TransactionId = null,
    string? RawRequest = null,
    string? RawResponse = null,
    MesExternalDataSource? DataSource = null);

public sealed record MesExternalDataSource(
    MesExternalDataSourceKind Kind,
    string Location,
    string? Format = null,
    DateTimeOffset? LastWriteTime = null);

public enum MesExternalDataSourceKind
{
    Unknown,
    File,
    Directory,
    Database,
    Memory
}