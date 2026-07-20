using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record LiveSnapshotResult(
    LiveSnapshotMachineResult Machine,
    int? RuntimeVersion,
    LiveSnapshotProductionLotResult? ProductionLot,
    LiveSnapshotWorkpieceResult? CurrentWorkpiece,
    LiveSnapshotOperationResult? CurrentOperation,
    IReadOnlyCollection<LiveSnapshotAlarmResult> ActiveAlarms,
    DateTimeOffset SnapshotAt
);

public sealed record LiveSnapshotMachineResult(
    string Id,
    string Name,
    MachineRuntimeStatus? Status,
    DateTimeOffset? LastChangedAt
);

public sealed record LiveSnapshotProductionLotResult(
    Guid Id,
    string Code,
    ProductionLotStatus Status,
    decimal ProgressPercentage,
    int CompletedOperations,
    int TotalOperations
);

public sealed record LiveSnapshotWorkpieceResult(
    Guid Id,
    string Code,
    WorkpieceStatus Status,
    int SequenceNumber,
    int Position,
    int TotalWorkpieces,
    decimal ProgressPercentage,
    int CompletedOperations,
    int TotalOperations
);

public sealed record LiveSnapshotOperationResult(
    Guid Id,
    MachineOperationType Type,
    MachineOperationStatus Status,
    int SequenceNumber,
    int Position,
    int TotalOperations,
    int ProgressPercentage,
    string? CurrentPhase,
    DateTimeOffset? StartedAt
);

public sealed record LiveSnapshotAlarmResult(
    Guid Id,
    string Code,
    MachineAlarmSeverity Severity,
    MachineAlarmStatus Status,
    string Message,
    bool IsBlocking,
    DateTimeOffset RaisedAt
);
