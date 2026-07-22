using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class MachineRuntimeAssignedOperationQuery
{
    private readonly IMachineRuntimeStateRepository _runtimeStateRepository;
    private readonly IMachineOperationRepository _operationRepository;
    private readonly ILogger<MachineRuntimeAssignedOperationQuery> _logger;

    public MachineRuntimeAssignedOperationQuery(
        IMachineRuntimeStateRepository runtimeStateRepository,
        IMachineOperationRepository operationRepository,
        ILogger<MachineRuntimeAssignedOperationQuery> logger
    )
    {
        ArgumentNullException.ThrowIfNull(runtimeStateRepository);
        ArgumentNullException.ThrowIfNull(operationRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _runtimeStateRepository = runtimeStateRepository;
        _operationRepository = operationRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<MachineOperation>> GetAssignedRunningOperationsAsync(
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineRuntimeState> runtimeStates =
            await _runtimeStateRepository.GetAllAsync(cancellationToken);

        List<MachineOperation> operations = [];

        foreach (
            MachineRuntimeState runtimeState in runtimeStates.Where(item =>
                item.Status == MachineRuntimeStatus.Running
            )
        )
        {
            if (runtimeState.CurrentOperationId is not Guid operationId)
            {
                _logger.LogWarning(
                    "Machine runtime {MachineId} is Running but has no current operation assignment.",
                    runtimeState.MachineId
                );
                continue;
            }

            MachineOperation? operation = await _operationRepository.GetByIdAsync(
                operationId,
                cancellationToken
            );

            if (operation is null)
            {
                _logger.LogWarning(
                    "Machine runtime {MachineId} references missing operation {OperationId}.",
                    runtimeState.MachineId,
                    operationId
                );
                continue;
            }

            if (operation.Status != MachineOperationStatus.Running)
            {
                _logger.LogWarning(
                    "Machine runtime {MachineId} references operation {OperationId} with status {OperationStatus}; expected Running.",
                    runtimeState.MachineId,
                    operation.Id,
                    operation.Status
                );
                continue;
            }

            if (!string.Equals(operation.MachineId, runtimeState.MachineId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Machine runtime {MachineId} references operation {OperationId} assigned to machine {OperationMachineId}.",
                    runtimeState.MachineId,
                    operation.Id,
                    operation.MachineId
                );
                continue;
            }

            operations.Add(operation);
        }

        return operations;
    }
}
