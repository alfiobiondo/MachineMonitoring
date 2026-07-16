using MachineMonitoring.Api.Operations;

namespace MachineMonitoring.Api.Production;

public sealed record WorkpieceDetailsResponse(
    Guid Id,
    Guid ProductionLotId,
    string Code,
    string MaterialCode,
    string Status,
    bool IsSequenceActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<MachineOperationResponse> Operations
);
