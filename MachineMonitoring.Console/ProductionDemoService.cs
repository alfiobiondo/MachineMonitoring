using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Console;

public sealed class ProductionDemoService
{
    private readonly MachineOperationApplicationService _applicationService;
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _operationRepository;
    private readonly ILogger<ProductionDemoService> _logger;

    public ProductionDemoService(
        MachineOperationApplicationService applicationService,
        IProductionLotRepository productionLotRepository,
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository operationRepository,
        ILogger<ProductionDemoService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(applicationService);
        ArgumentNullException.ThrowIfNull(productionLotRepository);
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(operationRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationService = applicationService;
        _productionLotRepository = productionLotRepository;
        _workpieceRepository = workpieceRepository;
        _operationRepository = operationRepository;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ProductionLot productionLot = new(
            id: Guid.NewGuid(),
            code: "LOT-DEMO-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );

        Guid workpieceId = Guid.NewGuid();
        Workpiece workpiece = new(
            id: workpieceId,
            productionLotId: productionLot.Id,
            sequenceNumber: 1,
            code: "WP-DEMO-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await _productionLotRepository.AddAsync(productionLot, cancellationToken);
        await _workpieceRepository.AddAsync(workpiece, cancellationToken);

        CreateLaserCutOperationCommand command = new(
            WorkpieceId: workpieceId,
            SequenceNumber: 1,
            MachineId: "M-001",
            MaterialId: InMemoryProductionData.StainlessSteel304MaterialId,
            NozzleId: InMemoryProductionData.Nozzle12Id,
            DrawingFileId: InMemoryProductionData.TubeDrawingId,
            Geometry: new TubeGeometryInput(
                OuterDiameterMillimeters: 80m,
                ThicknessMillimeters: 3m,
                LengthMillimeters: 6_000m
            ),
            LaserPowerWatts: 2_500m,
            CuttingSpeedMillimetersPerMinute: 1_200m,
            AssistGas: AssistGasType.Nitrogen,
            GasPressureBar: 15m,
            FocalOffsetMillimeters: -0.5m,
            NumberOfPasses: 1
        );

        CreateLaserCutOperationResult result =
            await _applicationService.CreateLaserCutOperationAsync(command, cancellationToken);

        _logger.LogInformation(
            "Created operation {OperationId}. "
                + "Status: {OperationStatus}. "
                + "Geometry: {GeometryType}.",
            result.OperationId,
            result.OperationStatus,
            result.GeometryType
        );

        await _applicationService.StartAsync(
            new StartMachineOperationCommand(
                OperationId: result.OperationId,
                InitialPhase: "Preparing laser"
            ),
            cancellationToken
        );

        await _applicationService.PauseAsync(
            new PauseMachineOperationCommand(result.OperationId),
            cancellationToken
        );

        await _applicationService.ResumeAsync(
            new ResumeMachineOperationCommand(result.OperationId),
            cancellationToken
        );

        MachineOperation? operation = await _operationRepository.GetByIdAsync(
            result.OperationId,
            cancellationToken
        );
        LaserCutConfiguration? configuration =
            await _operationRepository.GetConfigurationByOperationIdAsync(
                result.OperationId,
                cancellationToken
            );

        if (configuration is null)
        {
            throw new InvalidOperationException("The configuration was not stored.");
        }

        _logger.LogInformation(
            "Stored configuration: "
                + "{GeometryType}, "
                + "{LaserPowerWatts} W, "
                + "{CuttingSpeed} mm/min.",
            configuration.GeometryType,
            configuration.LaserPowerWatts,
            configuration.CuttingSpeedMillimetersPerMinute
        );

        if (operation is null)
        {
            throw new InvalidOperationException("The operation was not stored.");
        }

        _logger.LogInformation(
            "Operation {OperationId} final demo status: "
                + "{OperationStatus}. Progress: {ProgressPercentage}%.",
            operation.Id,
            operation.Status,
            operation.ProgressPercentage
        );
    }
}
