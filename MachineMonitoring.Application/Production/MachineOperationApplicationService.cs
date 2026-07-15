using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationApplicationService
{
    private readonly IMaterialRepository _materialRepository;
    private readonly INozzleRepository _nozzleRepository;
    private readonly IDrawingFileRepository _drawingFileRepository;
    private readonly IMachineCapabilitiesRepository _machineCapabilitiesRepository;

    private readonly IMachineOperationRepository _machineOperationRepository;

    private readonly LaserCutConfigurationValidator _configurationValidator;

    private readonly ILogger<MachineOperationApplicationService> _logger;

    public MachineOperationApplicationService(
        IMaterialRepository materialRepository,
        INozzleRepository nozzleRepository,
        IDrawingFileRepository drawingFileRepository,
        IMachineCapabilitiesRepository machineCapabilitiesRepository,
        IMachineOperationRepository machineOperationRepository,
        LaserCutConfigurationValidator configurationValidator,
        ILogger<MachineOperationApplicationService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(materialRepository);

        ArgumentNullException.ThrowIfNull(nozzleRepository);

        ArgumentNullException.ThrowIfNull(drawingFileRepository);

        ArgumentNullException.ThrowIfNull(machineCapabilitiesRepository);

        ArgumentNullException.ThrowIfNull(machineOperationRepository);

        ArgumentNullException.ThrowIfNull(configurationValidator);

        ArgumentNullException.ThrowIfNull(logger);

        _materialRepository = materialRepository;
        _nozzleRepository = nozzleRepository;
        _drawingFileRepository = drawingFileRepository;

        _machineCapabilitiesRepository = machineCapabilitiesRepository;

        _machineOperationRepository = machineOperationRepository;

        _configurationValidator = configurationValidator;

        _logger = logger;
    }

    public async Task<CreateLaserCutOperationResult> CreateLaserCutOperationAsync(
        CreateLaserCutOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidateCreateCommand(command);

        Material material = await GetRequiredMaterialAsync(command.MaterialId, cancellationToken);

        Nozzle nozzle = await GetRequiredNozzleAsync(command.NozzleId, cancellationToken);

        await EnsureDrawingFileExistsAsync(command.DrawingFileId, cancellationToken);

        MachineCapabilities capabilities = await GetRequiredCapabilitiesAsync(
            command.MachineId,
            cancellationToken
        );

        IWorkpieceGeometry geometry = CreateGeometry(command.Geometry);

        Guid operationId = Guid.NewGuid();
        Guid configurationId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        MachineOperation operation = new(
            id: operationId,
            workpieceId: command.WorkpieceId,
            machineId: command.MachineId,
            type: MachineOperationType.LaserCutting,
            createdAt: createdAt
        );

        LaserCutConfiguration configuration = new(
            id: configurationId,
            operationId: operationId,
            materialId: command.MaterialId,
            nozzleId: command.NozzleId,
            drawingFileId: command.DrawingFileId,
            geometry: geometry,
            laserPowerWatts: command.LaserPowerWatts,
            cuttingSpeedMillimetersPerMinute: command.CuttingSpeedMillimetersPerMinute,
            assistGas: command.AssistGas,
            gasPressureBar: command.GasPressureBar,
            focalOffsetMillimeters: command.FocalOffsetMillimeters,
            numberOfPasses: command.NumberOfPasses,
            createdAt: createdAt
        );

        _configurationValidator.Validate(configuration, material, nozzle, capabilities);

        await _machineOperationRepository.AddAsync(operation, configuration, cancellationToken);

        _logger.LogInformation(
            "Laser-cutting operation {OperationId} created "
                + "for workpiece {WorkpieceId} on machine {MachineId}. "
                + "Geometry type: {GeometryType}.",
            operation.Id,
            operation.WorkpieceId,
            operation.MachineId,
            configuration.GeometryType
        );

        return new CreateLaserCutOperationResult(
            OperationId: operation.Id,
            ConfigurationId: configuration.Id,
            OperationStatus: operation.Status,
            GeometryType: configuration.GeometryType
        );
    }

    public async Task StartAsync(
        StartMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: command.InitialPhase);

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation("Machine operation {OperationId} started.", operation.Id);
    }

    public async Task PauseAsync(
        PauseMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Pause();

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation("Machine operation {OperationId} paused.", operation.Id);
    }

    public async Task ResumeAsync(
        ResumeMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Resume();

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation("Machine operation {OperationId} resumed.", operation.Id);
    }

    public async Task UpdateProgressAsync(
        UpdateMachineOperationProgressCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.UpdateProgress(
            progressPercentage: command.ProgressPercentage,
            currentPhase: command.CurrentPhase
        );

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation(
            "Machine operation {OperationId} progress updated to "
                + "{ProgressPercentage}%. Current phase: {CurrentPhase}.",
            operation.Id,
            operation.ProgressPercentage,
            operation.CurrentPhase
        );
    }

    public async Task CompleteAsync(
        CompleteMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Complete(completedAt: DateTimeOffset.UtcNow);

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation("Machine operation {OperationId} completed.", operation.Id);
    }

    public async Task FailAsync(
        FailMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Fail(failureReason: command.FailureReason);

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogWarning(
            "Machine operation {OperationId} failed. Reason: {FailureReason}.",
            operation.Id,
            operation.FailureReason
        );
    }

    public async Task CancelAsync(
        CancelMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        MachineOperation operation = await GetRequiredOperationAsync(
            command.OperationId,
            cancellationToken
        );

        operation.Cancel();

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);

        _logger.LogInformation("Machine operation {OperationId} cancelled.", operation.Id);
    }

    private static void ValidateCreateCommand(CreateLaserCutOperationCommand command)
    {
        if (command.WorkpieceId == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(command));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.MachineId);

        ArgumentNullException.ThrowIfNull(command.Geometry);
    }

    private static IWorkpieceGeometry CreateGeometry(WorkpieceGeometryInput geometryInput)
    {
        return geometryInput switch
        {
            TubeGeometryInput tube => new TubeGeometry(
                outerDiameterMillimeters: tube.OuterDiameterMillimeters,
                thicknessMillimeters: tube.ThicknessMillimeters,
                lengthMillimeters: tube.LengthMillimeters
            ),

            SheetGeometryInput sheet => new SheetGeometry(
                widthMillimeters: sheet.WidthMillimeters,
                heightMillimeters: sheet.HeightMillimeters,
                thicknessMillimeters: sheet.ThicknessMillimeters
            ),

            _ => throw new ArgumentException(
                $"Unsupported geometry input type " + $"'{geometryInput.GetType().Name}'.",
                nameof(geometryInput)
            ),
        };
    }

    private async Task<Material> GetRequiredMaterialAsync(
        Guid materialId,
        CancellationToken cancellationToken
    )
    {
        Material? material = await _materialRepository.GetByIdAsync(materialId, cancellationToken);

        return material
            ?? throw new ResourceNotFoundException(
                resourceType: "Material",
                resourceId: materialId.ToString()
            );
    }

    private async Task<Nozzle> GetRequiredNozzleAsync(
        Guid nozzleId,
        CancellationToken cancellationToken
    )
    {
        Nozzle? nozzle = await _nozzleRepository.GetByIdAsync(nozzleId, cancellationToken);

        return nozzle
            ?? throw new ResourceNotFoundException(
                resourceType: "Nozzle",
                resourceId: nozzleId.ToString()
            );
    }

    private async Task EnsureDrawingFileExistsAsync(
        Guid drawingFileId,
        CancellationToken cancellationToken
    )
    {
        DrawingFile? drawingFile = await _drawingFileRepository.GetByIdAsync(
            drawingFileId,
            cancellationToken
        );

        if (drawingFile is null)
        {
            throw new ResourceNotFoundException(
                resourceType: "Drawing file",
                resourceId: drawingFileId.ToString()
            );
        }
    }

    private async Task<MachineCapabilities> GetRequiredCapabilitiesAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        MachineCapabilities? capabilities =
            await _machineCapabilitiesRepository.GetByMachineIdAsync(machineId, cancellationToken);

        return capabilities
            ?? throw new ResourceNotFoundException(
                resourceType: "Machine capabilities",
                resourceId: machineId
            );
    }

    private async Task<MachineOperation> GetRequiredOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        MachineOperation? operation = await _machineOperationRepository.GetByIdAsync(
            operationId,
            cancellationToken
        );

        return operation
            ?? throw new ResourceNotFoundException(
                resourceType: "Machine operation",
                resourceId: operationId.ToString()
            );
    }
}
