using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class WorkpieceApplicationService
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly ProductionSequenceService _productionSequenceService;

    public WorkpieceApplicationService(
        IProductionLotRepository productionLotRepository,
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository machineOperationRepository,
        ProductionSequenceService productionSequenceService
    )
    {
        ArgumentNullException.ThrowIfNull(productionLotRepository);
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(productionSequenceService);

        _productionLotRepository = productionLotRepository;
        _workpieceRepository = workpieceRepository;
        _machineOperationRepository = machineOperationRepository;
        _productionSequenceService = productionSequenceService;
    }

    public async Task<CreateWorkpieceResult> CreateAsync(
        CreateWorkpieceCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ProductionLotId == Guid.Empty)
        {
            throw new ArgumentException(
                "The production lot ID cannot be empty.",
                nameof(command)
            );
        }

        if (command.SequenceNumber <= 0)
        {
            throw new ArgumentException(
                "The workpiece sequence number must be greater than zero.",
                nameof(command)
            );
        }

        ProductionLot productionLot = await GetRequiredProductionLotAsync(
            command.ProductionLotId,
            cancellationToken
        );

        if (
            productionLot.Status
            is ProductionLotStatus.Completed
                or ProductionLotStatus.Failed
                or ProductionLotStatus.Cancelled
        )
        {
            throw new BusinessRuleViolationException(
                $"Production lot {productionLot.Code} cannot accept new workpieces from status {productionLot.Status}."
            );
        }

        IReadOnlyCollection<Domain.Production.Workpiece> existingWorkpieces =
            await _workpieceRepository.GetByProductionLotIdAsync(
                command.ProductionLotId,
                cancellationToken
            );

        if (existingWorkpieces.Any(item => item.SequenceNumber == command.SequenceNumber))
        {
            throw new BusinessRuleViolationException(
                $"Production lot {productionLot.Code} already contains workpiece sequence {command.SequenceNumber}."
            );
        }

        Workpiece workpiece = new(
            id: Guid.NewGuid(),
            productionLotId: command.ProductionLotId,
            sequenceNumber: command.SequenceNumber,
            code: command.Code,
            materialCode: command.MaterialCode,
            createdAt: DateTimeOffset.UtcNow
        );

        await _workpieceRepository.AddAsync(workpiece, cancellationToken);

        return new CreateWorkpieceResult(
            WorkpieceId: workpiece.Id,
            ProductionLotId: workpiece.ProductionLotId,
            SequenceNumber: workpiece.SequenceNumber,
            Code: workpiece.Code,
            MaterialCode: workpiece.MaterialCode,
            Status: workpiece.Status
        );
    }

    public Task StartAsync(StartWorkpieceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _productionSequenceService.StartWorkpieceAsync(
            command.WorkpieceId,
            command.InitialPhase,
            command.StartFromSequenceNumber,
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

        Workpiece workpiece = await _workpieceRepository.GetByIdAsync(
            workpieceId,
            cancellationToken
        )
            ?? throw new ResourceNotFoundException("Workpiece", workpieceId.ToString());

        IReadOnlyCollection<MachineOperation> operations =
            await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                workpieceId,
                cancellationToken
            );

        return new WorkpieceDetailsResult(
            Id: workpiece.Id,
            ProductionLotId: workpiece.ProductionLotId,
            SequenceNumber: workpiece.SequenceNumber,
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

    private async Task<ProductionLot> GetRequiredProductionLotAsync(
        Guid productionLotId,
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

        return await _productionLotRepository.GetByIdAsync(productionLotId, cancellationToken)
            ?? throw new ResourceNotFoundException("Production lot", productionLotId.ToString());
    }
}
