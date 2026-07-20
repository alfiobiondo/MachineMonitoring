using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record CreateProductionLotResult(
    Guid ProductionLotId,
    string Code,
    int PlannedQuantity,
    ProductionLotStatus Status
);
