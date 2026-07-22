namespace MachineMonitoring.Api.Production;

public sealed record CreateProductionLotResponse(
    Guid ProductionLotId,
    string Code,
    int PlannedQuantity,
    string Status
);
