using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineOperationDetailsResult(
    Guid Id,
    Guid WorkpieceId,
    string MachineId,
    MachineOperationType Type,
    MachineOperationStatus Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    LaserCutConfigurationDetailsResult Configuration
);
