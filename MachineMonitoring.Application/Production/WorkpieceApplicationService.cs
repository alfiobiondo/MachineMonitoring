using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;

namespace MachineMonitoring.Application.Production;

public sealed class WorkpieceApplicationService
{
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly ProductionSequenceService _productionSequenceService;

    public WorkpieceApplicationService(
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository machineOperationRepository,
        ProductionSequenceService productionSequenceService
    )
    {
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(productionSequenceService);

        _workpieceRepository = workpieceRepository;
        _machineOperationRepository = machineOperationRepository;
        _productionSequenceService = productionSequenceService;
    }

    public Task StartAsync(StartWorkpieceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _productionSequenceService.StartWorkpieceAsync(
            command.WorkpieceId,
            command.InitialPhase,
            cancellationToken
        );
    }

    public async Task<WorkpieceDetailsResult> GetDetailsAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        if (workpieceId == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(workpieceId));
        }

        Domain.Production.Workpiece workpiece = await _workpieceRepository.GetByIdAsync(
            workpieceId,
            cancellationToken
        )
            ?? throw new ResourceNotFoundException("Workpiece", workpieceId.ToString());

        IReadOnlyCollection<Domain.Production.MachineOperation> operations =
            await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                workpieceId,
                cancellationToken
            );

        return new WorkpieceDetailsResult(
            Id: workpiece.Id,
            ProductionLotId: workpiece.ProductionLotId,
            Code: workpiece.Code,
            MaterialCode: workpiece.MaterialCode,
            Status: workpiece.Status,
            IsSequenceActive: workpiece.IsSequenceActive,
            CreatedAt: workpiece.CreatedAt,
            StartedAt: workpiece.StartedAt,
            CompletedAt: workpiece.CompletedAt,
            Operations: operations.Select(ProductionMappings.ToSummaryResult).ToArray()
        );
    }
}
