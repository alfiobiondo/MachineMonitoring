namespace MachineMonitoring.Api.Operations;

public sealed record MachineOperationDetailsResponse(
    Guid Id,
    Guid WorkpieceId,
    string MachineId,
    string Type,
    string Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    LaserCutConfigurationResponse Configuration
);
