using MachineMonitoring.Application.Common;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestMachineOperationRepository : IMachineOperationRepository
{
    private readonly Dictionary<Guid, MachineOperation> _operations = [];
    private readonly Dictionary<Guid, LaserCutConfiguration> _configurations = [];

    private readonly object _syncRoot = new();

    public void Seed(MachineOperation operation, LaserCutConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_syncRoot)
        {
            _operations[operation.Id] = operation;

            if (configuration is not null)
            {
                _configurations[operation.Id] = configuration;
            }
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _operations.Clear();
            _configurations.Clear();
        }
    }

    public bool TryGetValue(Guid operationId, out MachineOperation? operation)
    {
        lock (_syncRoot)
        {
            bool found = _operations.TryGetValue(operationId, out MachineOperation? storedOperation);
            operation = storedOperation;
            return found;
        }
    }

    public Task<MachineOperation?> GetByIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _operations.TryGetValue(operationId, out MachineOperation? operation);

            return Task.FromResult(operation);
        }
    }

    public Task<PagedResult<MachineOperation>> GetAllAsync(
        string? machineId,
        MachineOperationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IEnumerable<MachineOperation> query = _operations.Values;

            if (!string.IsNullOrWhiteSpace(machineId))
            {
                query = query.Where(operation =>
                    string.Equals(
                        operation.MachineId,
                        machineId,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            }

            if (status is not null)
            {
                query = query.Where(operation => operation.Status == status.Value);
            }

            MachineOperation[] filteredOperations = query
                .OrderByDescending(operation => operation.CreatedAt)
                .ThenByDescending(operation => operation.Id)
                .ToArray();

            MachineOperation[] pageItems = filteredOperations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            PagedResult<MachineOperation> result = new(
                Items: pageItems,
                Page: page,
                PageSize: pageSize,
                TotalItems: filteredOperations.Length
            );

            return Task.FromResult(result);
        }
    }

    public Task<LaserCutConfiguration?> GetConfigurationByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _configurations.TryGetValue(operationId, out LaserCutConfiguration? configuration);

            return Task.FromResult(configuration);
        }
    }

    public Task AddAsync(
        MachineOperation operation,
        LaserCutConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_syncRoot)
        {
            _operations.Add(operation.Id, operation);
            _configurations.Add(operation.Id, configuration);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MachineOperation>> GetOrderedByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyCollection<MachineOperation> result = _operations
                .Values.Where(operation => operation.WorkpieceId == workpieceId)
                .OrderBy(operation => operation.SequenceNumber)
                .ThenBy(operation => operation.Id)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<bool> ExistsIncompletePredecessorAsync(
        Guid workpieceId,
        int sequenceNumber,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            bool exists = _operations.Values.Any(operation =>
                operation.WorkpieceId == workpieceId
                && operation.SequenceNumber < sequenceNumber
                && operation.Status != MachineOperationStatus.Completed
                && operation.Status != MachineOperationStatus.Skipped
            );

            return Task.FromResult(exists);
        }
    }

    public Task<MachineOperation?> GetFirstExecutableQueuedByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            MachineOperation? operation = _operations
                .Values.Where(operation =>
                    operation.WorkpieceId == workpieceId
                    && operation.Status == MachineOperationStatus.Queued
                )
                .OrderBy(operation => operation.SequenceNumber)
                .ThenBy(operation => operation.Id)
                .FirstOrDefault();

            return Task.FromResult(operation);
        }
    }

    public Task<IReadOnlyCollection<MachineOperation>> GetRunningOperationsAsync(
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyCollection<MachineOperation> operations = _operations
                .Values.Where(operation => operation.Status == MachineOperationStatus.Running)
                .OrderBy(operation => operation.WorkpieceId)
                .ThenBy(operation => operation.SequenceNumber)
                .ToArray();

            return Task.FromResult(operations);
        }
    }

    public Task UpdateAsync(MachineOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(operation);

        lock (_syncRoot)
        {
            if (!_operations.ContainsKey(operation.Id))
            {
                throw new InvalidOperationException($"Operation {operation.Id} does not exist.");
            }

            _operations[operation.Id] = operation;
        }

        return Task.CompletedTask;
    }
}
