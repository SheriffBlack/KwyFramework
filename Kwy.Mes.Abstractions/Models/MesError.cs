namespace Kwy.Mes.Abstractions.Models;

public sealed record MesError(
    string Code,
    string Message,
    string? Detail = null);
