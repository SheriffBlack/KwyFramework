namespace Kwy.Mes.Abstractions.Models;

public sealed record MesRouteCheckResult(
    bool Allowed,
    string Code,
    string Message,
    string? NextStep = null);
