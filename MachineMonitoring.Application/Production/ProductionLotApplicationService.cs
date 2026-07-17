using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;

namespace MachineMonitoring.Application.Production;

public sealed class ProductionLotApplicationService
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly ProductionSequenceService _productionSequenceService;

    public ProductionLotApplicationService(
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

    public Task StartAsync(StartProductionLotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _productionSequenceService.StartProductionLotAsync(
            command.ProductionLotId,
            command.InitialPhase,
            command.StartFromWorkpieceSequenceNumber,
            cancellationToken
        );
    }

    public async Task<ProductionLotDetailsResult> GetDetailsAsync(
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

        Domain.Production.ProductionLot productionLot =
            await _productionLotRepository.GetByIdAsync(productionLotId, cancellationToken)
            ?? throw new ResourceNotFoundException("Production lot", productionLotId.ToString());

        IReadOnlyCollection<Domain.Production.Workpiece> workpieces =
            await _workpieceRepository.GetByProductionLotIdAsync(productionLotId, cancellationToken);

        List<WorkpieceDetailsResult> workpieceResults = [];

        foreach (
            Domain.Production.Workpiece workpiece in workpieces.OrderBy(item => item.SequenceNumber)
        )
        {
            IReadOnlyCollection<Domain.Production.MachineOperation> operations =
                await _machineOperationRepository.GetOrderedByWorkpieceIdAsync(
                    workpiece.Id,
                    cancellationToken
                );

            workpieceResults.Add(
                new WorkpieceDetailsResult(
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
                )
            );
        }

        return new ProductionLotDetailsResult(
            Id: productionLot.Id,
            Code: productionLot.Code,
            PlannedQuantity: productionLot.PlannedQuantity,
            Status: productionLot.Status,
            CreatedAt: productionLot.CreatedAt,
            StartedAt: productionLot.StartedAt,
            CompletedAt: productionLot.CompletedAt,
            Workpieces: workpieceResults
        );
    }
}
