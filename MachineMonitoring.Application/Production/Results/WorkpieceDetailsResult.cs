using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record WorkpieceDetailsResult(
    Guid Id,
    Guid ProductionLotId,
    int SequenceNumber,
    string Code,
    string MaterialCode,
    WorkpieceStatus Status,
    bool IsSequenceActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<MachineOperationSummaryResult> Operations
);
