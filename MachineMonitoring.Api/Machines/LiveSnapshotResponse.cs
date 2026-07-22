namespace MachineMonitoring.Api.Machines;

public sealed record LiveSnapshotResponse(
    LiveSnapshotMachineResponse Machine,
    int? RuntimeVersion,
    LiveSnapshotProductionLotResponse? ProductionLot,
    LiveSnapshotWorkpieceResponse? CurrentWorkpiece,
    LiveSnapshotOperationResponse? CurrentOperation,
    IReadOnlyCollection<LiveSnapshotAlarmResponse> ActiveAlarms,
    IReadOnlyCollection<LiveSnapshotWarningResponse> Warnings,
    DateTimeOffset SnapshotAt
);

public sealed record LiveSnapshotMachineResponse(
    string Id,
    string Name,
    string? Status,
    DateTimeOffset? LastChangedAt
);

public sealed record LiveSnapshotProductionLotResponse(
    Guid Id,
    string Code,
    string Status,
    decimal ProgressPercentage,
    int CompletedOperations,
    int TotalOperations
);

public sealed record LiveSnapshotWorkpieceResponse(
    Guid Id,
    string Code,
    string Status,
    int SequenceNumber,
    int Position,
    int TotalWorkpieces,
    decimal ProgressPercentage,
    int CompletedOperations,
    int TotalOperations
);

public sealed record LiveSnapshotOperationResponse(
    Guid Id,
    string Type,
    string Status,
    int SequenceNumber,
    int Position,
    int TotalOperations,
    int ProgressPercentage,
    string? CurrentPhase,
    DateTimeOffset? StartedAt
);

public sealed record LiveSnapshotAlarmResponse(
    Guid Id,
    string Code,
    string Severity,
    string Status,
    string Message,
    bool IsBlocking,
    DateTimeOffset RaisedAt
);

public sealed record LiveSnapshotWarningResponse(
    string Id,
    string MachineId,
    string Code,
    string Severity,
    string Title,
    string Message,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ResolvedAt,
    bool IsActive,
    string? SourceId
);
