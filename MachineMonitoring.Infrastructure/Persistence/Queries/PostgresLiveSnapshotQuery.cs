using MachineMonitoring.Application;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain;
using MachineMonitoring.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Queries;

public sealed class PostgresLiveSnapshotQuery : ILiveSnapshotQuery
{
    private readonly MachineMonitoringDbContext _dbContext;
    private readonly IMachineProvider _machineProvider;
    private readonly TimeProvider _timeProvider;

    public PostgresLiveSnapshotQuery(
        MachineMonitoringDbContext dbContext,
        IMachineProvider machineProvider,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(machineProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _machineProvider = machineProvider;
        _timeProvider = timeProvider;
    }

    public async Task<LiveSnapshotResult> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        Machine machine = (await _machineProvider.GetMachinesAsync(cancellationToken))
            .SingleOrDefault(item =>
                string.Equals(item.Id, machineId, StringComparison.OrdinalIgnoreCase)
            )
            ?? throw new ResourceNotFoundException("Machine", machineId);

        RuntimeSnapshotProjection? runtimeSnapshot = await _dbContext
            .MachineRuntimeStates.AsNoTracking()
            .Where(item => item.MachineId == machine.Id)
            .Select(item => new RuntimeSnapshotProjection(
                MachineId: item.MachineId,
                Status: item.Status,
                LastChangedAt: item.LastChangedAt,
                Version: item.Version,
                CurrentOperationId: item.CurrentOperationId,
                CurrentOperationType: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Type,
                CurrentOperationStatus: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Status,
                CurrentOperationMachineId: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.MachineId,
                CurrentOperationSequenceNumber: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.SequenceNumber,
                CurrentOperationPosition: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.Operations.Count(operation =>
                        operation.SequenceNumber <= item.CurrentOperation!.SequenceNumber
                    ),
                CurrentOperationTotalOperations: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.Operations.Count(),
                CurrentOperationProgressPercentage: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.ProgressPercentage,
                CurrentOperationCurrentPhase: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.CurrentPhase,
                CurrentOperationStartedAt: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.StartedAt,
                CurrentWorkpieceId: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.WorkpieceId,
                CurrentWorkpieceCode: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.Code,
                CurrentWorkpieceStatus: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.Status,
                CurrentWorkpieceSequenceNumber: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.SequenceNumber,
                CurrentWorkpiecePosition: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.ProductionLot.Workpieces.Count(workpiece =>
                        workpiece.SequenceNumber <= item.CurrentOperation!.Workpiece.SequenceNumber
                    ),
                TotalWorkpieces: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.ProductionLot.Workpieces.Count(),
                ProductionLotId: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.ProductionLotId,
                ProductionLotCode: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.ProductionLot.Code,
                ProductionLotStatus: item.CurrentOperationId == null
                    ? null
                    : item.CurrentOperation!.Workpiece.ProductionLot.Status
            ))
            .SingleOrDefaultAsync(cancellationToken);

        AlarmProjection[] alarmRecords = await _dbContext
            .MachineAlarms.AsNoTracking()
            .Where(item =>
                item.MachineId == machine.Id && item.Status != MachineAlarmStatus.Resolved
            )
            .OrderBy(item => item.RaisedAt)
            .ThenBy(item => item.Id)
            .Select(item => new AlarmProjection(
                Id: item.Id,
                Code: item.Code,
                Severity: item.Severity,
                Status: item.Status,
                Message: item.Message,
                RaisedAt: item.RaisedAt
            ))
            .ToArrayAsync(cancellationToken);

        LiveSnapshotAlarmResult[] activeAlarms = alarmRecords
            .Select(item => new LiveSnapshotAlarmResult(
                Id: item.Id,
                Code: item.Code,
                Severity: item.Severity,
                Status: item.Status,
                Message: item.Message,
                IsBlocking: MachineAlarmBlockingPolicy.IsBlockingSeverity(item.Severity),
                RaisedAt: item.RaisedAt
            ))
            .ToArray();

        LiveSnapshotWarningResult[] warnings = await GetWarningsAsync(
            machine.Id,
            runtimeSnapshot,
            cancellationToken
        );

        LiveSnapshotMachineResult machineResult = new(
            Id: machine.Id,
            Name: machine.Name,
            Status: runtimeSnapshot?.Status,
            LastChangedAt: runtimeSnapshot?.LastChangedAt
        );

        if (runtimeSnapshot is null || runtimeSnapshot.CurrentOperationId is null)
        {
            return new LiveSnapshotResult(
                Machine: machineResult,
                RuntimeVersion: runtimeSnapshot?.Version,
                ProductionLot: null,
                CurrentWorkpiece: null,
                CurrentOperation: null,
                ActiveAlarms: activeAlarms,
                Warnings: warnings,
                SnapshotAt: _timeProvider.GetUtcNow()
            );
        }

        Guid productionLotId = runtimeSnapshot.ProductionLotId
            ?? throw new InvalidOperationException(
                $"Runtime state for machine {machine.Id} references an incomplete production hierarchy."
            );

        LotOperationProjection[] lotOperations = await _dbContext
            .MachineOperations.AsNoTracking()
            .Where(item => item.Workpiece.ProductionLotId == productionLotId)
            .OrderBy(item => item.Workpiece.SequenceNumber)
            .ThenBy(item => item.WorkpieceId)
            .ThenBy(item => item.SequenceNumber)
            .ThenBy(item => item.Id)
            .Select(item => new LotOperationProjection(
                OperationId: item.Id,
                WorkpieceId: item.WorkpieceId,
                WorkpieceCode: item.Workpiece.Code,
                WorkpieceStatus: item.Workpiece.Status,
                WorkpieceSequenceNumber: item.Workpiece.SequenceNumber,
                OperationType: item.Type,
                OperationStatus: item.Status,
                OperationSequenceNumber: item.SequenceNumber,
                OperationProgressPercentage: item.ProgressPercentage
            ))
            .ToArrayAsync(cancellationToken);

        LiveProgressAggregate productionLotAggregate = LiveSnapshotMath.CalculateAggregateProgress(
            lotOperations.Select(item => new LiveOperationAggregateInput(
                Status: item.OperationStatus,
                ProgressPercentage: item.OperationProgressPercentage
            ))
        );

        Guid currentWorkpieceId = runtimeSnapshot.CurrentWorkpieceId
            ?? throw new InvalidOperationException(
                $"Runtime state for machine {machine.Id} does not expose the current workpiece."
            );

        LotOperationProjection[] currentWorkpieceOperations = lotOperations
            .Where(item => item.WorkpieceId == currentWorkpieceId)
            .ToArray();

        LiveProgressAggregate currentWorkpieceAggregate =
            LiveSnapshotMath.CalculateAggregateProgress(
                currentWorkpieceOperations.Select(item => new LiveOperationAggregateInput(
                    Status: item.OperationStatus,
                    ProgressPercentage: item.OperationProgressPercentage
                ))
            );

        LiveSnapshotProductionLotResult productionLotResult = new(
            Id: productionLotId,
            Code: runtimeSnapshot.ProductionLotCode
                ?? throw new InvalidOperationException(
                    $"Production lot {productionLotId} does not expose a code."
                ),
            Status: runtimeSnapshot.ProductionLotStatus
                ?? throw new InvalidOperationException(
                    $"Production lot {productionLotId} does not expose a status."
                ),
            ProgressPercentage: productionLotAggregate.ProgressPercentage,
            CompletedOperations: productionLotAggregate.CompletedOperations,
            TotalOperations: productionLotAggregate.TotalOperations
        );

        LiveSnapshotWorkpieceResult currentWorkpieceResult = new(
            Id: currentWorkpieceId,
            Code: runtimeSnapshot.CurrentWorkpieceCode
                ?? throw new InvalidOperationException(
                    $"Workpiece {currentWorkpieceId} does not expose a code."
                ),
            Status: runtimeSnapshot.CurrentWorkpieceStatus
                ?? throw new InvalidOperationException(
                    $"Workpiece {currentWorkpieceId} does not expose a status."
                ),
            SequenceNumber: runtimeSnapshot.CurrentWorkpieceSequenceNumber
                ?? throw new InvalidOperationException(
                    $"Workpiece {currentWorkpieceId} does not expose a sequence number."
                ),
            Position: runtimeSnapshot.CurrentWorkpiecePosition
                ?? throw new InvalidOperationException(
                    $"Workpiece {currentWorkpieceId} does not expose a position."
                ),
            TotalWorkpieces: runtimeSnapshot.TotalWorkpieces
                ?? throw new InvalidOperationException(
                    $"Workpiece {currentWorkpieceId} does not expose the total workpieces."
                ),
            ProgressPercentage: currentWorkpieceAggregate.ProgressPercentage,
            CompletedOperations: currentWorkpieceAggregate.CompletedOperations,
            TotalOperations: currentWorkpieceAggregate.TotalOperations
        );

        LiveSnapshotOperationResult currentOperationResult = new(
            Id: runtimeSnapshot.CurrentOperationId.Value,
            Type: runtimeSnapshot.CurrentOperationType
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose a type."
                ),
            Status: runtimeSnapshot.CurrentOperationStatus
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose a status."
                ),
            SequenceNumber: runtimeSnapshot.CurrentOperationSequenceNumber
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose a sequence number."
                ),
            Position: runtimeSnapshot.CurrentOperationPosition
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose a position."
                ),
            TotalOperations: runtimeSnapshot.CurrentOperationTotalOperations
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose total operations."
                ),
            ProgressPercentage: runtimeSnapshot.CurrentOperationProgressPercentage
                ?? throw new InvalidOperationException(
                    $"Operation {runtimeSnapshot.CurrentOperationId.Value} does not expose progress."
                ),
            CurrentPhase: runtimeSnapshot.CurrentOperationCurrentPhase,
            StartedAt: runtimeSnapshot.CurrentOperationStartedAt
        );

        return new LiveSnapshotResult(
            Machine: machineResult,
            RuntimeVersion: runtimeSnapshot.Version,
            ProductionLot: productionLotResult,
            CurrentWorkpiece: currentWorkpieceResult,
            CurrentOperation: currentOperationResult,
            ActiveAlarms: activeAlarms,
            Warnings: warnings,
            SnapshotAt: _timeProvider.GetUtcNow()
        );
    }

    private async Task<LiveSnapshotWarningResult[]> GetWarningsAsync(
        string machineId,
        RuntimeSnapshotProjection? runtimeSnapshot,
        CancellationToken cancellationToken
    )
    {
        List<LiveSnapshotWarningResult> warnings = [];

        if (
            runtimeSnapshot?.Status == MachineRuntimeStatus.Running
            && runtimeSnapshot.CurrentOperationId is null
        )
        {
            warnings.Add(
                new LiveSnapshotWarningResult(
                    Id: $"{machineId}:RuntimeWithoutCurrentOperation",
                    MachineId: machineId,
                    Code: "RuntimeWithoutCurrentOperation",
                    Severity: "Warning",
                    Title: "Runtime senza operation corrente",
                    Message: "La macchina risulta Running, ma il runtime non punta a una operation corrente.",
                    DetectedAt: runtimeSnapshot.LastChangedAt,
                    ResolvedAt: null,
                    IsActive: true,
                    SourceId: null
                )
            );
        }

        if (
            runtimeSnapshot?.CurrentOperationId is Guid currentOperationId
            && runtimeSnapshot.CurrentOperationMachineId is string currentOperationMachineId
            && !string.Equals(currentOperationMachineId, machineId, StringComparison.OrdinalIgnoreCase)
        )
        {
            warnings.Add(
                new LiveSnapshotWarningResult(
                    Id: $"{machineId}:RuntimeOperationMismatch:{currentOperationId}",
                    MachineId: machineId,
                    Code: "RuntimeOperationMismatch",
                    Severity: "Warning",
                    Title: "Operation assegnata a un'altra macchina",
                    Message:
                        $"Il runtime punta all'operation {currentOperationId}, assegnata alla macchina {currentOperationMachineId}.",
                    DetectedAt: runtimeSnapshot.LastChangedAt,
                    ResolvedAt: null,
                    IsActive: true,
                    SourceId: currentOperationId.ToString()
                )
            );
        }

        Guid? runtimeOperationId = runtimeSnapshot?.CurrentOperationId;

        OrphanRunningOperationProjection[] orphanRunningOperations = await _dbContext
            .MachineOperations.AsNoTracking()
            .Where(operation =>
                operation.MachineId == machineId
                && operation.Status == MachineOperationStatus.Running
                && (!runtimeOperationId.HasValue || operation.Id != runtimeOperationId.Value)
            )
            .OrderBy(operation => operation.StartedAt)
            .ThenBy(operation => operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .Select(operation => new OrphanRunningOperationProjection(
                OperationId: operation.Id,
                StartedAt: operation.StartedAt,
                CreatedAt: operation.CreatedAt
            ))
            .ToArrayAsync(cancellationToken);

        warnings.AddRange(
            orphanRunningOperations.Select(operation => new LiveSnapshotWarningResult(
                Id: $"{machineId}:OrphanRunningOperation:{operation.OperationId}",
                MachineId: machineId,
                Code: "OrphanRunningOperation",
                Severity: "Warning",
                Title: "Operation running non assegnata",
                Message:
                    $"L'operation {operation.OperationId} risulta Running sulla macchina, ma non e' l'operation corrente del runtime.",
                DetectedAt: operation.StartedAt ?? operation.CreatedAt,
                ResolvedAt: null,
                IsActive: true,
                SourceId: operation.OperationId.ToString()
            ))
        );

        return warnings.ToArray();
    }

    private sealed record RuntimeSnapshotProjection(
        string MachineId,
        MachineRuntimeStatus Status,
        DateTimeOffset LastChangedAt,
        int Version,
        Guid? CurrentOperationId,
        MachineOperationType? CurrentOperationType,
        MachineOperationStatus? CurrentOperationStatus,
        string? CurrentOperationMachineId,
        int? CurrentOperationSequenceNumber,
        int? CurrentOperationPosition,
        int? CurrentOperationTotalOperations,
        int? CurrentOperationProgressPercentage,
        string? CurrentOperationCurrentPhase,
        DateTimeOffset? CurrentOperationStartedAt,
        Guid? CurrentWorkpieceId,
        string? CurrentWorkpieceCode,
        WorkpieceStatus? CurrentWorkpieceStatus,
        int? CurrentWorkpieceSequenceNumber,
        int? CurrentWorkpiecePosition,
        int? TotalWorkpieces,
        Guid? ProductionLotId,
        string? ProductionLotCode,
        ProductionLotStatus? ProductionLotStatus
    );

    private sealed record LotOperationProjection(
        Guid OperationId,
        Guid WorkpieceId,
        string WorkpieceCode,
        WorkpieceStatus WorkpieceStatus,
        int WorkpieceSequenceNumber,
        MachineOperationType OperationType,
        MachineOperationStatus OperationStatus,
        int OperationSequenceNumber,
        int OperationProgressPercentage
    );

    private sealed record AlarmProjection(
        Guid Id,
        string Code,
        MachineAlarmSeverity Severity,
        MachineAlarmStatus Status,
        string Message,
        DateTimeOffset RaisedAt
    );

    private sealed record OrphanRunningOperationProjection(
        Guid OperationId,
        DateTimeOffset? StartedAt,
        DateTimeOffset CreatedAt
    );
}
