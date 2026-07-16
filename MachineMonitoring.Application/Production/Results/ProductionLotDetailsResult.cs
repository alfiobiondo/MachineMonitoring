using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record ProductionLotDetailsResult(
    Guid Id,
    string Code,
    int PlannedQuantity,
    ProductionLotStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<WorkpieceDetailsResult> Workpieces
);
