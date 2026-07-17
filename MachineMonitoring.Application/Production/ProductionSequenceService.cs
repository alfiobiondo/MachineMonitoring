using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class ProductionSequenceService
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IMachineOperationEventRepository _machineOperationEventRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly ILogger<ProductionSequenceService> _logger;

    public ProductionSequenceService(
        IProductionLotRepository productionLotRepository,
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository machineOperationRepository,
        IMachineOperationEventRepository machineOperationEventRepository,
        IProductionTransactionManager transactionManager,
        ILogger<ProductionSequenceService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(productionLotRepository);
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(machineOperationEventRepository);
        ArgumentNullException.ThrowIfNull(transactionManager);
        ArgumentNullException.ThrowIfNull(logger);

        _productionLotRepository = productionLotRepository;
        _workpieceRepository = workpieceRepository;
        _machineOperationRepository = machineOperationRepository;
        _machineOperationEventRepository = machineOperationEventRepository;
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public Task StartWorkpieceAsync(
        Guid workpieceId,
        string initialPhase,
        int? startFromSequenceNumber,
        CancellationToken cancellationToken
    )
    {
        if (workpieceId == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(workpieceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(initialPhase);

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                Workpiece workpiece = await GetRequiredWorkpieceAsync(workpieceId, ct);
                ProductionLot productionLot = await GetRequiredProductionLotAsync(
                    workpiece.ProductionLotId,
                    ct
                );

                productionLot.Start(DateTimeOffset.UtcNow);
                await _productionLotRepository.UpdateAsync(productionLot, ct);

                workpiece.StartSequence(DateTimeOffset.UtcNow);
                await _workpieceRepository.UpdateAsync(workpiece, ct);

                if (startFromSequenceNumber is int selectedSequenceNumber)
                {
                    await StartWorkpieceFromSequenceAsync(
                        workpiece,
                        selectedSequenceNumber,
                        initialPhase,
                        ct
                    );
                }
                else
                {
                    await TryStartFirstQueuedOperationAsync(workpiece, initialPhase, ct);
                }

                await UpdateProductionLotStatusAsync(productionLot.Id, ct);
            },
            cancellationToken
        );
    }

    public Task StartProductionLotAsync(
        Guid productionLotId,
        string initialPhase,
        int? startFromWorkpieceSequenceNumber,
        CancellationToken cancellationToken
    )
    {
        if (productionLotId == Guid.Empty)
        {
            throw new ArgumentException(
                "The production lot ID cannot be empty.",
                nameof(productionLotId)
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(initialPhase);

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                ProductionLot productionLot = await GetRequiredProductionLotAsync(
                    productionLotId,
                    ct
                );

                IReadOnlyCollection<Workpiece> workpieces =
                    await _workpieceRepository.GetByProductionLotIdAsync(productionLotId, ct);

                if (
                    startFromWorkpieceSequenceNumber is int requestedWorkpieceSequenceNumber
                    && !workpieces.Any(item =>
                        item.SequenceNumber == requestedWorkpieceSequenceNumber
                    )
                )
                {
                    throw new ResourceNotFoundException(
                        "Workpiece sequence",
                        $"{productionLotId}/{requestedWorkpieceSequenceNumber}"
                    );
                }

                productionLot.Start(DateTimeOffset.UtcNow);
                await _productionLotRepository.UpdateAsync(productionLot, ct);

                foreach (Workpiece workpiece in workpieces.OrderBy(item => item.SequenceNumber))
                {
                    if (
                        workpiece.Status
                        is WorkpieceStatus.Completed
                            or WorkpieceStatus.Failed
                            or WorkpieceStatus.Cancelled
                            or WorkpieceStatus.Skipped
                    )
                    {
                        continue;
                    }

                    if (
                        startFromWorkpieceSequenceNumber is int selectedWorkpieceSequenceNumber
                        && workpiece.SequenceNumber < selectedWorkpieceSequenceNumber
                    )
                    {
                        await SkipPendingWorkpieceAsync(workpiece, ct);
                        continue;
                    }

                    workpiece.StartSequence(DateTimeOffset.UtcNow);
                    await _workpieceRepository.UpdateAsync(workpiece, ct);

                    await TryStartFirstQueuedOperationAsync(workpiece, initialPhase, ct);
                }

                await UpdateProductionLotStatusAsync(productionLotId, ct);
            },
            cancellationToken
        );
    }

    public Task HandleOperationCompletedAsync(
        MachineOperation operation,
        string initialPhase,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialPhase);

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                Workpiece workpiece = await GetRequiredWorkpieceAsync(operation.WorkpieceId, ct);
                IReadOnlyCollection<MachineOperation> operations =
                    await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                        workpiece.Id,
                        ct
                    );

                bool hasRemainingQueued = operations.Any(item =>
                    item.Status == MachineOperationStatus.Queued
                );

                if (workpiece.IsSequenceActive && hasRemainingQueued)
                {
                    await TryStartFirstQueuedOperationAsync(workpiece, initialPhase, ct);
                }

                operations = await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                    workpiece.Id,
                    ct
                );

                if (
                    operations.Count > 0
                    && operations.All(item =>
                        item.Status
                            is MachineOperationStatus.Completed
                                or MachineOperationStatus.Skipped
                    )
                )
                {
                    workpiece.Complete(DateTimeOffset.UtcNow);
                    await _workpieceRepository.UpdateAsync(workpiece, ct);
                }

                await UpdateProductionLotStatusAsync(workpiece.ProductionLotId, ct);
            },
            cancellationToken
        );
    }

    public Task HandleOperationBlockedAsync(Guid operationId, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("The operation ID cannot be empty.", nameof(operationId));
        }

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(operationId, ct);
                Workpiece workpiece = await GetRequiredWorkpieceAsync(operation.WorkpieceId, ct);

                if (operation.Status == MachineOperationStatus.Failed)
                {
                    workpiece.Fail(DateTimeOffset.UtcNow);
                }
                else if (operation.Status == MachineOperationStatus.Cancelled)
                {
                    workpiece.Cancel(DateTimeOffset.UtcNow);
                }
                else if (operation.Status == MachineOperationStatus.Faulted)
                {
                    _logger.LogInformation(
                        "Operation {OperationId} faulted. Workpiece {WorkpieceId} remains sequence-active: {IsSequenceActive}.",
                        operation.Id,
                        workpiece.Id,
                        workpiece.IsSequenceActive
                    );
                }
                else
                {
                    throw new BusinessRuleViolationException(
                        $"Operation {operation.Id} does not block the workpiece sequence from status {operation.Status}."
                    );
                }

                await _workpieceRepository.UpdateAsync(workpiece, ct);
                await UpdateProductionLotStatusAsync(workpiece.ProductionLotId, ct);
            },
            cancellationToken
        );
    }

    public async Task EnsureOperationCanStartAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        bool hasIncompletePredecessor =
            await _machineOperationRepository.ExistsIncompletePredecessorAsync(
                operation.WorkpieceId,
                operation.SequenceNumber,
                cancellationToken
            );

        if (hasIncompletePredecessor)
        {
            throw new BusinessRuleViolationException(
                $"Operation {operation.Id} cannot be started because a previous operation of workpiece {operation.WorkpieceId} is not completed."
            );
        }
    }

    private async Task TryStartFirstQueuedOperationAsync(
        Workpiece workpiece,
        string initialPhase,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineOperation> operations =
            await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                workpiece.Id,
                cancellationToken
            );

        if (
            operations.Any(operation =>
                operation.Status
                    is MachineOperationStatus.Running
                        or MachineOperationStatus.Paused
                        or MachineOperationStatus.Faulted
            )
        )
        {
            return;
        }

        MachineOperation? operationToStart =
            await _machineOperationRepository.GetFirstExecutableQueuedByWorkpieceIdAsync(
                workpiece.Id,
                cancellationToken
            );

        if (operationToStart is null)
        {
            return;
        }

        await EnsureOperationCanStartAsync(operationToStart, cancellationToken);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        operationToStart.Start(startedAt, initialPhase);

        await _machineOperationRepository.UpdateAsync(operationToStart, cancellationToken);
        await AppendEventAsync(
            operationToStart,
            MachineOperationEventType.Started,
            previousStatus: MachineOperationStatus.Queued,
            newStatus: operationToStart.Status,
            reason: null,
            occurredAt: startedAt,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation(
            "Started queued operation {OperationId} for workpiece {WorkpieceId} as part of an active sequence.",
            operationToStart.Id,
            workpiece.Id
        );
    }

    private async Task StartWorkpieceFromSequenceAsync(
        Workpiece workpiece,
        int startFromSequenceNumber,
        string initialPhase,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineOperation> operations =
            await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                workpiece.Id,
                cancellationToken
            );

        MachineOperation? targetOperation = operations.SingleOrDefault(item =>
            item.SequenceNumber == startFromSequenceNumber
        );

        if (targetOperation is null)
        {
            throw new ResourceNotFoundException(
                "Machine operation sequence",
                $"{workpiece.Id}/{startFromSequenceNumber}"
            );
        }

        foreach (
            MachineOperation operation in operations.Where(item =>
                item.SequenceNumber < startFromSequenceNumber
            )
        )
        {
            if (operation.Status == MachineOperationStatus.Queued)
            {
                DateTimeOffset skippedAt = DateTimeOffset.UtcNow;
                operation.Skip(skippedAt, "Skipped by partial workpiece start.");
                await _machineOperationRepository.UpdateAsync(operation, cancellationToken);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Skipped,
                    previousStatus: MachineOperationStatus.Queued,
                    newStatus: operation.Status,
                    reason: operation.FailureReason,
                    occurredAt: skippedAt,
                    cancellationToken: cancellationToken
                );
                continue;
            }

            if (
                operation.Status
                is not MachineOperationStatus.Completed
                    and not MachineOperationStatus.Skipped
            )
            {
                throw new BusinessRuleViolationException(
                    $"Operation {operation.Id} cannot be skipped from status {operation.Status} while starting workpiece {workpiece.Id} from sequence {startFromSequenceNumber}."
                );
            }
        }

        await EnsureOperationCanStartAsync(targetOperation, cancellationToken);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        targetOperation.Start(startedAt, initialPhase);
        await _machineOperationRepository.UpdateAsync(targetOperation, cancellationToken);
        await AppendEventAsync(
            targetOperation,
            MachineOperationEventType.Started,
            previousStatus: MachineOperationStatus.Queued,
            newStatus: targetOperation.Status,
            reason: null,
            occurredAt: startedAt,
            cancellationToken: cancellationToken
        );
    }

    private async Task SkipPendingWorkpieceAsync(
        Workpiece workpiece,
        CancellationToken cancellationToken
    )
    {
        if (workpiece.Status != WorkpieceStatus.Pending)
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {workpiece.Id} cannot be skipped from status {workpiece.Status}."
            );
        }

        IReadOnlyCollection<MachineOperation> operations =
            await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                workpiece.Id,
                cancellationToken
            );

        foreach (MachineOperation operation in operations)
        {
            if (operation.Status != MachineOperationStatus.Queued)
            {
                continue;
            }

            DateTimeOffset skippedAt = DateTimeOffset.UtcNow;
            operation.Skip(skippedAt, "Skipped by partial production lot start.");
            await _machineOperationRepository.UpdateAsync(operation, cancellationToken);
            await AppendEventAsync(
                operation,
                MachineOperationEventType.Skipped,
                previousStatus: MachineOperationStatus.Queued,
                newStatus: operation.Status,
                reason: operation.FailureReason,
                occurredAt: skippedAt,
                cancellationToken: cancellationToken
            );
        }

        workpiece.Skip(DateTimeOffset.UtcNow);
        await _workpieceRepository.UpdateAsync(workpiece, cancellationToken);
    }

    private Task AppendEventAsync(
        MachineOperation operation,
        MachineOperationEventType eventType,
        MachineOperationStatus? previousStatus,
        MachineOperationStatus? newStatus,
        string? reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken
    )
    {
        MachineOperationEvent machineOperationEvent = new(
            id: Guid.NewGuid(),
            machineOperationId: operation.Id,
            eventType: eventType,
            occurredAt: occurredAt,
            previousStatus: previousStatus,
            newStatus: newStatus,
            progressPercentage: operation.ProgressPercentage,
            phase: operation.CurrentPhase,
            reason: reason,
            machineAlarmId: null,
            metadata: null
        );

        return _machineOperationEventRepository.AddAsync(machineOperationEvent, cancellationToken);
    }

    private async Task UpdateProductionLotStatusAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        ProductionLot productionLot = await GetRequiredProductionLotAsync(
            productionLotId,
            cancellationToken
        );
        IReadOnlyCollection<Workpiece> workpieces =
            await _workpieceRepository.GetByProductionLotIdAsync(
                productionLotId,
                cancellationToken
            );

        if (workpieces.Count == 0)
        {
            return;
        }

        bool allCompleted = workpieces.All(item =>
            item.Status is WorkpieceStatus.Completed or WorkpieceStatus.Skipped
        );
        bool allTerminal = workpieces.All(item =>
            item.Status
                is WorkpieceStatus.Completed
                    or WorkpieceStatus.Skipped
                    or WorkpieceStatus.Failed
                    or WorkpieceStatus.Cancelled
        );
        bool hasFailedOrCancelled = workpieces.Any(item =>
            item.Status is WorkpieceStatus.Failed or WorkpieceStatus.Cancelled
        );

        if (allCompleted)
        {
            if (productionLot.Status != ProductionLotStatus.Completed)
            {
                productionLot.Complete(DateTimeOffset.UtcNow);
                await _productionLotRepository.UpdateAsync(productionLot, cancellationToken);
            }

            return;
        }

        if (allTerminal && hasFailedOrCancelled)
        {
            if (productionLot.Status != ProductionLotStatus.Failed)
            {
                productionLot.Fail(DateTimeOffset.UtcNow);
                await _productionLotRepository.UpdateAsync(productionLot, cancellationToken);
            }

            return;
        }

        if (
            productionLot.Status == ProductionLotStatus.Planned
            && workpieces.Any(item =>
                item.Status is WorkpieceStatus.Running or WorkpieceStatus.Pending
            )
        )
        {
            productionLot.Start(DateTimeOffset.UtcNow);
            await _productionLotRepository.UpdateAsync(productionLot, cancellationToken);
        }
    }

    private async Task<MachineOperation> GetRequiredOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        return await _machineOperationRepository.GetByIdAsync(operationId, cancellationToken)
            ?? throw new ResourceNotFoundException("Machine operation", operationId.ToString());
    }

    private async Task<Workpiece> GetRequiredWorkpieceAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        return await _workpieceRepository.GetByIdAsync(workpieceId, cancellationToken)
            ?? throw new ResourceNotFoundException("Workpiece", workpieceId.ToString());
    }

    private async Task<ProductionLot> GetRequiredProductionLotAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        return await _productionLotRepository.GetByIdAsync(productionLotId, cancellationToken)
            ?? throw new ResourceNotFoundException("Production lot", productionLotId.ToString());
    }
}
