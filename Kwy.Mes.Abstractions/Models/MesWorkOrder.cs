namespace Kwy.Mes.Abstractions.Models;

public sealed record MesWorkOrder(
    string WorkOrderNo,
    string ProductCode,
    int PlannedQuantity,
    int CompletedQuantity = 0,
    string? RecipeName = null,
    string? Revision = null);
