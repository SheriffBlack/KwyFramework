namespace Kwy.Mes.Abstractions.Models;

public sealed record MesTraceRecord(
    MesUnit Unit,
    MesStation Station,
    DateTimeOffset Time,
    string EventName,
    IReadOnlyDictionary<string, string> Values);
