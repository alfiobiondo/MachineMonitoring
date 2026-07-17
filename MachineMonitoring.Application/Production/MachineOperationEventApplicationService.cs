using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationEventApplicationService
{
    private readonly IMachineOperationEventRepository _eventRepository;
    private readonly IMachineOperationRepository _operationRepository;
    private readonly IWorkpieceRepository _workpieceRepository;

    public MachineOperationEventApplicationService(
        IMachineOperationEventRepository eventRepository,
        IMachineOperationRepository operationRepository,
        IWorkpieceRepository workpieceRepository
    )
    {
        _eventRepository = eventRepository;
        _operationRepository = operationRepository;
        _workpieceRepository = workpieceRepository;
    }

    public async Task<IReadOnlyCollection<MachineOperationEventResult>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineOperationEvent> events =
            await _eventRepository.GetByOperationIdAsync(operationId, cancellationToken);

        return await MapResultsAsync(events, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MachineOperationEventResult>> GetByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineOperationEvent> events =
            await _eventRepository.GetByWorkpieceIdAsync(workpieceId, cancellationToken);

        return await MapResultsAsync(events, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MachineOperationEventResult>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineOperationEvent> events =
            await _eventRepository.GetByProductionLotIdAsync(productionLotId, cancellationToken);

        return await MapResultsAsync(events, cancellationToken);
    }

    private async Task<IReadOnlyCollection<MachineOperationEventResult>> MapResultsAsync(
        IReadOnlyCollection<MachineOperationEvent> events,
        CancellationToken cancellationToken
    )
    {
        List<MachineOperationEventResult> results = [];

        foreach (MachineOperationEvent item in events.OrderBy(eventItem => eventItem.OccurredAt))
        {
            MachineOperation operation = await _operationRepository.GetByIdAsync(
                item.MachineOperationId,
                cancellationToken
            ) ?? throw new ResourceNotFoundException(
                "Machine operation",
                item.MachineOperationId.ToString()
            );

            Workpiece workpiece = await _workpieceRepository.GetByIdAsync(
                operation.WorkpieceId,
                cancellationToken
            ) ?? throw new ResourceNotFoundException("Workpiece", operation.WorkpieceId.ToString());

            results.Add(
                new MachineOperationEventResult(
                    Id: item.Id,
                    MachineOperationId: item.MachineOperationId,
                    WorkpieceId: workpiece.Id,
                    ProductionLotId: workpiece.ProductionLotId,
                    OperationSequenceNumber: operation.SequenceNumber,
                    WorkpieceSequenceNumber: workpiece.SequenceNumber,
                    EventType: item.EventType,
                    OccurredAt: item.OccurredAt,
                    PreviousStatus: item.PreviousStatus,
                    NewStatus: item.NewStatus,
                    ProgressPercentage: item.ProgressPercentage,
                    Phase: item.Phase,
                    Reason: item.Reason,
                    MachineAlarmId: item.MachineAlarmId,
                    Metadata: item.Metadata
                )
            );
        }

        return results;
    }
}
