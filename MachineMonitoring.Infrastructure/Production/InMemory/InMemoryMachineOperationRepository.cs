using MachineMonitoring.Application.Common;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryMachineOperationRepository : IMachineOperationRepository
{
    private readonly Dictionary<Guid, MachineOperation> _operations = new();

    private readonly Dictionary<Guid, LaserCutConfiguration> _configurationsByOperationId = new();

    private readonly object _syncRoot = new();

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

    public Task AddAsync(
        MachineOperation operation,
        LaserCutConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);

        cancellationToken.ThrowIfCancellationRequested();

        if (configuration.OperationId != operation.Id)
        {
            throw new ArgumentException(
                "The configuration does not belong to the supplied operation.",
                nameof(configuration)
            );
        }

        lock (_syncRoot)
        {
            if (_operations.ContainsKey(operation.Id))
            {
                throw new InvalidOperationException($"Operation {operation.Id} already exists.");
            }

            _operations.Add(operation.Id, operation);

            _configurationsByOperationId.Add(operation.Id, configuration);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(MachineOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        cancellationToken.ThrowIfCancellationRequested();

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

    public Task<LaserCutConfiguration?> GetConfigurationByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _configurationsByOperationId.TryGetValue(
                operationId,
                out LaserCutConfiguration? configuration
            );

            return Task.FromResult(configuration);
        }
    }
}
