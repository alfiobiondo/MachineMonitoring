namespace MachineMonitoring.Api.Production;

public sealed record CreateProductionLotRequest(string Code, int PlannedQuantity);
