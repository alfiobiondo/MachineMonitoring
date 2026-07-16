namespace MachineMonitoring.Api.Production;

public sealed record ProductionLotDetailsResponse(
    Guid Id,
    string Code,
    int PlannedQuantity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<WorkpieceDetailsResponse> Workpieces
);
