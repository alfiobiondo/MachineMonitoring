namespace MachineMonitoring.Application.Production.Commands;

public sealed record CreateProductionLotCommand(string Code, int PlannedQuantity);
