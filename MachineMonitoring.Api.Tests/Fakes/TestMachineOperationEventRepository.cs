using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestMachineOperationEventRepository : IMachineOperationEventRepository
{
    private readonly List<MachineOperationEvent> _items = [];
    private readonly TestMachineOperationRepository _operationRepository;
    private readonly TestWorkpieceRepository _workpieceRepository;

    public TestMachineOperationEventRepository(
        TestMachineOperationRepository operationRepository,
        TestWorkpieceRepository workpieceRepository
    )
    {
        _operationRepository = operationRepository;
        _workpieceRepository = workpieceRepository;
    }

    public Task AddAsync(
        MachineOperationEvent machineOperationEvent,
        CancellationToken cancellationToken
    )
    {
        _items.Add(machineOperationEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MachineOperationEvent>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>(
            _items.Where(item => item.MachineOperationId == operationId)
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.Id)
                .ToArray()
        );
    }

    public Task<IReadOnlyCollection<MachineOperationEvent>> GetByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>(
            _items.Where(item =>
                    _operationRepository.TryGetValue(
                        item.MachineOperationId,
                        out MachineOperation? operation
                    )
                    && operation is not null
                    && operation.WorkpieceId == workpieceId
                )
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.Id)
                .ToArray()
        );
    }

    public Task<IReadOnlyCollection<MachineOperationEvent>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>(
            _items.Where(item =>
                    _operationRepository.TryGetValue(
                        item.MachineOperationId,
                        out MachineOperation? operation
                    )
                    && operation is not null
                    && _workpieceRepository.TryGetValue(
                        operation.WorkpieceId,
                        out Workpiece? workpiece
                    )
                    && workpiece is not null
                    && workpiece.ProductionLotId == productionLotId
                )
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.Id)
                .ToArray()
        );
    }

    public void Clear() => _items.Clear();
}
