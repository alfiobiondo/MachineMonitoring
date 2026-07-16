using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class ProductionSequenceService
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly ILogger<ProductionSequenceService> _logger;

    public ProductionSequenceService(
        IProductionLotRepository productionLotRepository,
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository machineOperationRepository,
        IProductionTransactionManager transactionManager,
        ILogger<ProductionSequenceService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(productionLotRepository);
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(transactionManager);
        ArgumentNullException.ThrowIfNull(logger);

        _productionLotRepository = productionLotRepository;
        _workpieceRepository = workpieceRepository;
        _machineOperationRepository = machineOperationRepository;
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public Task StartWorkpieceAsync(
        Guid workpieceId,
        string initialPhase,
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

                await TryStartFirstQueuedOperationAsync(workpiece, initialPhase, ct);

                await UpdateProductionLotStatusAsync(productionLot.Id, ct);
            },
            cancellationToken
        );
    }

    public Task StartProductionLotAsync(
        Guid productionLotId,
        string initialPhase,
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

                productionLot.Start(DateTimeOffset.UtcNow);
                await _productionLotRepository.UpdateAsync(productionLot, ct);

                IReadOnlyCollection<Workpiece> workpieces =
                    await _workpieceRepository.GetByProductionLotIdAsync(productionLotId, ct);

                foreach (Workpiece workpiece in workpieces)
                {
                    if (
                        workpiece.Status
                        is WorkpieceStatus.Completed or WorkpieceStatus.Failed or WorkpieceStatus.Cancelled
                    )
                    {
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

                if (operations.Count > 0 && operations.All(item => item.Status == MachineOperationStatus.Completed))
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
                operation.Status is MachineOperationStatus.Running or MachineOperationStatus.Paused
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

        operationToStart.Start(DateTimeOffset.UtcNow, initialPhase);

        await _machineOperationRepository.UpdateAsync(operationToStart, cancellationToken);

        _logger.LogInformation(
            "Started queued operation {OperationId} for workpiece {WorkpieceId} as part of an active sequence.",
            operationToStart.Id,
            workpiece.Id
        );
    }

    private async Task UpdateProductionLotStatusAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        ProductionLot productionLot = await GetRequiredProductionLotAsync(productionLotId, cancellationToken);
        IReadOnlyCollection<Workpiece> workpieces =
            await _workpieceRepository.GetByProductionLotIdAsync(productionLotId, cancellationToken);

        if (workpieces.Count == 0)
        {
            return;
        }

        bool allCompleted = workpieces.All(item => item.Status == WorkpieceStatus.Completed);
        bool allTerminal = workpieces.All(item =>
            item.Status is WorkpieceStatus.Completed or WorkpieceStatus.Failed or WorkpieceStatus.Cancelled
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
