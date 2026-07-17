using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Exceptions;
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
    private readonly IWorkpieceRepository _workpieceRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IMachineOperationEventRepository _machineOperationEventRepository;
    private readonly IMachineAlarmRepository _machineAlarmRepository;
    private readonly IMachineRuntimeStateRepository _machineRuntimeStateRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly ProductionSequenceService _productionSequenceService;
    private readonly LaserCutConfigurationValidator _configurationValidator;
    private readonly IProductionNotificationPublisher _notificationPublisher;
    private readonly ILogger<MachineOperationApplicationService> _logger;

    public MachineOperationApplicationService(
        IMaterialRepository materialRepository,
        INozzleRepository nozzleRepository,
        IDrawingFileRepository drawingFileRepository,
        IMachineCapabilitiesRepository machineCapabilitiesRepository,
        IWorkpieceRepository workpieceRepository,
        IMachineOperationRepository machineOperationRepository,
        IMachineOperationEventRepository machineOperationEventRepository,
        IMachineAlarmRepository machineAlarmRepository,
        IMachineRuntimeStateRepository machineRuntimeStateRepository,
        IProductionTransactionManager transactionManager,
        ProductionSequenceService productionSequenceService,
        LaserCutConfigurationValidator configurationValidator,
        IProductionNotificationPublisher notificationPublisher,
        ILogger<MachineOperationApplicationService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(materialRepository);

        ArgumentNullException.ThrowIfNull(nozzleRepository);

        ArgumentNullException.ThrowIfNull(drawingFileRepository);
        ArgumentNullException.ThrowIfNull(machineCapabilitiesRepository);
        ArgumentNullException.ThrowIfNull(workpieceRepository);
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(machineOperationEventRepository);
        ArgumentNullException.ThrowIfNull(machineAlarmRepository);
        ArgumentNullException.ThrowIfNull(machineRuntimeStateRepository);
        ArgumentNullException.ThrowIfNull(transactionManager);
        ArgumentNullException.ThrowIfNull(productionSequenceService);
        ArgumentNullException.ThrowIfNull(configurationValidator);
        ArgumentNullException.ThrowIfNull(notificationPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _materialRepository = materialRepository;
        _nozzleRepository = nozzleRepository;
        _drawingFileRepository = drawingFileRepository;
        _machineCapabilitiesRepository = machineCapabilitiesRepository;
        _workpieceRepository = workpieceRepository;
        _machineOperationRepository = machineOperationRepository;
        _machineOperationEventRepository = machineOperationEventRepository;
        _machineAlarmRepository = machineAlarmRepository;
        _machineRuntimeStateRepository = machineRuntimeStateRepository;
        _transactionManager = transactionManager;
        _productionSequenceService = productionSequenceService;
        _configurationValidator = configurationValidator;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<CreateLaserCutOperationResult> CreateLaserCutOperationAsync(
        CreateLaserCutOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidateCreateCommand(command);

        await GetRequiredWorkpieceAsync(command.WorkpieceId, cancellationToken);

        Material material = await GetRequiredMaterialAsync(command.MaterialId, cancellationToken);

        Nozzle nozzle = await GetRequiredNozzleAsync(command.NozzleId, cancellationToken);

        await GetRequiredDrawingFileAsync(command.DrawingFileId, cancellationToken);

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
            sequenceNumber: command.SequenceNumber,
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

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                await _machineOperationRepository.AddAsync(operation, configuration, ct);

                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct
                );

                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Created,
                    previousStatus: null,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
            },
            cancellationToken
        );

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
            SequenceNumber: operation.SequenceNumber,
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

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );

                await _productionSequenceService.EnsureOperationCanStartAsync(operation, ct);

                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                await EnsureMachineCanAcceptOperationAsync(runtimeState, operation, ct);

                DateTimeOffset startedAt = DateTimeOffset.UtcNow;
                int expectedVersion = runtimeState.Version;
                operation.Start(startedAt: startedAt, initialPhase: command.InitialPhase);
                runtimeState.StartOperation(operation.Id, startedAt);

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Started,
                    previousStatus: MachineOperationStatus.Queued,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogInformation("Machine operation {OperationId} started.", operation.Id);
            },
            cancellationToken
        );
    }

    public async Task PauseAsync(
        PauseMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                int expectedVersion = runtimeState.Version;
                operation.Pause();
                runtimeState.PauseOperation(operation.Id, DateTimeOffset.UtcNow);

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Paused,
                    previousStatus: MachineOperationStatus.Running,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogInformation("Machine operation {OperationId} paused.", operation.Id);
            },
            cancellationToken
        );
    }

    public async Task ResumeAsync(
        ResumeMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                await EnsureMachineCanResumeOperationAsync(runtimeState, operation, ct);

                int expectedVersion = runtimeState.Version;
                operation.Resume();
                runtimeState.ResumeOperation(operation.Id, DateTimeOffset.UtcNow);

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Resumed,
                    previousStatus: MachineOperationStatus.Paused,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogInformation("Machine operation {OperationId} resumed.", operation.Id);
            },
            cancellationToken
        );
    }

    public async Task UpdateProgressAsync(
        UpdateMachineOperationProgressCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                if (
                    runtimeState.Status != MachineRuntimeStatus.Running
                    || runtimeState.CurrentOperationId != operation.Id
                )
                {
                    throw new BusinessRuleViolationException(
                        $"Operation {operation.Id} cannot advance while machine {operation.MachineId} is {runtimeState.Status}."
                    );
                }

                operation.UpdateProgress(
                    progressPercentage: command.ProgressPercentage,
                    currentPhase: command.CurrentPhase
                );

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await _notificationPublisher.PublishAsync(
                    new OperationProgressChangedNotification(
                        OperationId: operation.Id,
                        ProgressPercentage: operation.ProgressPercentage,
                        CurrentPhase: operation.CurrentPhase,
                        OccurredAt: DateTimeOffset.UtcNow
                    ),
                    ct
                );

                _logger.LogInformation(
                    "Machine operation {OperationId} progress updated to {ProgressPercentage}%. Current phase: {CurrentPhase}.",
                    operation.Id,
                    operation.ProgressPercentage,
                    operation.CurrentPhase
                );
            },
            cancellationToken
        );
    }

    public async Task CompleteAsync(
        CompleteMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                DateTimeOffset completedAt = DateTimeOffset.UtcNow;
                int expectedVersion = runtimeState.Version;
                operation.Complete(completedAt: completedAt);
                runtimeState.CompleteOperation(operation.Id, completedAt);

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Completed,
                    previousStatus: MachineOperationStatus.Running,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await _productionSequenceService.HandleOperationCompletedAsync(
                    operation,
                    initialPhase: "Preparing laser",
                    ct
                );

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogInformation("Machine operation {OperationId} completed.", operation.Id);
            },
            cancellationToken
        );
    }

    public async Task FailAsync(
        FailMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                int expectedVersion = runtimeState.Version;
                operation.Fail(failureReason: command.FailureReason);

                IReadOnlyCollection<MachineAlarm> alarms =
                    await _machineAlarmRepository.GetByMachineIdAsync(
                        operation.MachineId,
                        activeOnly: true,
                        ct
                    );

                if (alarms.Any(MachineAlarmBlockingPolicy.IsBlocking))
                {
                    runtimeState.Fault(
                        operationId: operation.Id,
                        alarmId: alarms.First(MachineAlarmBlockingPolicy.IsBlocking).Id,
                        failureReason: command.FailureReason,
                        changedAt: DateTimeOffset.UtcNow
                    );
                }
                else
                {
                    runtimeState.SetAvailable(DateTimeOffset.UtcNow);
                }

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Failed,
                    previousStatus: MachineOperationStatus.Running,
                    newStatus: operation.Status,
                    reason: operation.FailureReason,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await _productionSequenceService.HandleOperationBlockedAsync(operation.Id, ct);

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogWarning(
                    "Machine operation {OperationId} failed. Reason: {FailureReason}.",
                    operation.Id,
                    operation.FailureReason
                );
            },
            cancellationToken
        );
    }

    public async Task CancelAsync(
        CancelMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                int expectedVersion = runtimeState.Version;
                operation.Cancel();
                runtimeState.SetAvailable(DateTimeOffset.UtcNow);

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Cancelled,
                    previousStatus: null,
                    newStatus: operation.Status,
                    reason: null,
                    machineAlarmId: null,
                    cancellationToken: ct
                );

                await _productionSequenceService.HandleOperationBlockedAsync(operation.Id, ct);

                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
                _logger.LogInformation("Machine operation {OperationId} cancelled.", operation.Id);
            },
            cancellationToken
        );
    }

    public Task FaultAsync(
        FaultMachineOperationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation operation = await GetRequiredOperationAsync(
                    command.OperationId,
                    ct
                );
                MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
                    operation.MachineId,
                    ct,
                    operation
                );

                int expectedVersion = runtimeState.Version;
                operation.Fault(command.FailureReason);
                MachineAlarm alarm = new(
                    id: Guid.NewGuid(),
                    machineId: operation.MachineId,
                    machineOperationId: operation.Id,
                    code: command.AlarmCode,
                    severity: command.Severity,
                    message: command.AlarmMessage,
                    raisedAt: DateTimeOffset.UtcNow
                );

                runtimeState.Fault(
                    operationId: operation.Id,
                    alarmId: alarm.Id,
                    failureReason: command.FailureReason,
                    changedAt: alarm.RaisedAt
                );

                await _machineOperationRepository.UpdateAsync(operation, ct);
                await _machineAlarmRepository.AddAsync(alarm, ct);
                await SaveRuntimeStateAsync(runtimeState, expectedVersion, ct);
                await AppendEventAsync(
                    operation,
                    MachineOperationEventType.Faulted,
                    previousStatus: MachineOperationStatus.Running,
                    newStatus: operation.Status,
                    reason: command.FailureReason,
                    machineAlarmId: alarm.Id,
                    cancellationToken: ct
                );

                await _productionSequenceService.HandleOperationBlockedAsync(operation.Id, ct);
                await _notificationPublisher.PublishAsync(
                    new MachineAlarmRaisedNotification(
                        AlarmId: alarm.Id,
                        MachineId: alarm.MachineId,
                        OperationId: alarm.MachineOperationId,
                        OccurredAt: alarm.RaisedAt
                    ),
                    ct
                );
                await PublishOperationStatusNotificationAsync(operation, ct);
                await PublishMachineRuntimeNotificationAsync(runtimeState, ct);
            },
            cancellationToken
        );
    }

    public async Task<MachineOperationDetailsResult> GetDetailsAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("The operation ID cannot be empty.", nameof(operationId));
        }

        MachineOperation operation = await GetRequiredOperationAsync(
            operationId,
            cancellationToken
        );

        LaserCutConfiguration? configuration =
            await _machineOperationRepository.GetConfigurationByOperationIdAsync(
                operationId,
                cancellationToken
            );

        if (configuration is null)
        {
            throw new ResourceNotFoundException(
                resourceType: "Laser-cut configuration",
                resourceId: operationId.ToString()
            );
        }

        Material material = await GetRequiredMaterialAsync(
            configuration.MaterialId,
            cancellationToken
        );

        Nozzle nozzle = await GetRequiredNozzleAsync(configuration.NozzleId, cancellationToken);

        DrawingFile drawingFile = await GetRequiredDrawingFileAsync(
            configuration.DrawingFileId,
            cancellationToken
        );
        MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
            operation.MachineId,
            cancellationToken
        );
        IReadOnlyCollection<MachineAlarm> activeAlarms =
            await _machineAlarmRepository.GetByMachineIdAsync(
                operation.MachineId,
                activeOnly: true,
                cancellationToken
            );
        MachineAlarm? activeBlockingAlarm = activeAlarms.FirstOrDefault(
            MachineAlarmBlockingPolicy.IsBlocking
        );

        WorkpieceGeometryDetailsResult geometry = CreateGeometryDetails(configuration.Geometry);

        LaserCutConfigurationDetailsResult configurationResult = new(
            Id: configuration.Id,
            MaterialId: material.Id,
            MaterialCode: material.Code,
            MaterialName: material.Name,
            NozzleId: nozzle.Id,
            NozzleCode: nozzle.Code,
            DrawingFileId: drawingFile.Id,
            DrawingFileName: drawingFile.OriginalFileName,
            Geometry: geometry,
            LaserPowerWatts: configuration.LaserPowerWatts,
            CuttingSpeedMillimetersPerMinute: configuration.CuttingSpeedMillimetersPerMinute,
            AssistGas: configuration.AssistGas,
            GasPressureBar: configuration.GasPressureBar,
            FocalOffsetMillimeters: configuration.FocalOffsetMillimeters,
            NumberOfPasses: configuration.NumberOfPasses,
            CreatedAt: configuration.CreatedAt
        );

        return new MachineOperationDetailsResult(
            Id: operation.Id,
            WorkpieceId: operation.WorkpieceId,
            SequenceNumber: operation.SequenceNumber,
            MachineId: operation.MachineId,
            Type: operation.Type,
            Status: operation.Status,
            ProgressPercentage: operation.ProgressPercentage,
            CurrentPhase: operation.CurrentPhase,
            FailureReason: operation.FailureReason,
            MachineRuntimeStatus: runtimeState.Status,
            ActiveBlockingAlarm: activeBlockingAlarm is null
                ? null
                : ToAlarmResult(activeBlockingAlarm),
            CanResume: CanResume(operation, runtimeState, activeAlarms),
            CanPause: operation.Status == MachineOperationStatus.Running,
            CanFault: operation.Status == MachineOperationStatus.Running,
            CreatedAt: operation.CreatedAt,
            StartedAt: operation.StartedAt,
            CompletedAt: operation.CompletedAt,
            Configuration: configurationResult
        );
    }

    private static void ValidateCreateCommand(CreateLaserCutOperationCommand command)
    {
        if (command.WorkpieceId == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(command));
        }

        if (command.SequenceNumber <= 0)
        {
            throw new ArgumentException(
                "The sequence number must be greater than zero.",
                nameof(command)
            );
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

    private async Task<DrawingFile> GetRequiredDrawingFileAsync(
        Guid drawingFileId,
        CancellationToken cancellationToken
    )
    {
        DrawingFile? drawingFile = await _drawingFileRepository.GetByIdAsync(
            drawingFileId,
            cancellationToken
        );

        return drawingFile
            ?? throw new ResourceNotFoundException(
                resourceType: "Drawing file",
                resourceId: drawingFileId.ToString()
            );
    }

    private async Task<Workpiece> GetRequiredWorkpieceAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        Workpiece? workpiece = await _workpieceRepository.GetByIdAsync(
            workpieceId,
            cancellationToken
        );

        return workpiece
            ?? throw new ResourceNotFoundException(
                resourceType: "Workpiece",
                resourceId: workpieceId.ToString()
            );
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

    private static WorkpieceGeometryDetailsResult CreateGeometryDetails(IWorkpieceGeometry geometry)
    {
        return geometry switch
        {
            TubeGeometry tube => new TubeGeometryDetailsResult(
                OuterDiameterMillimeters: tube.OuterDiameterMillimeters,
                ThicknessMillimeters: tube.ThicknessMillimeters,
                LengthMillimeters: tube.LengthMillimeters,
                InnerDiameterMillimeters: tube.InnerDiameterMillimeters
            ),

            SheetGeometry sheet => new SheetGeometryDetailsResult(
                WidthMillimeters: sheet.WidthMillimeters,
                HeightMillimeters: sheet.HeightMillimeters,
                ThicknessMillimeters: sheet.ThicknessMillimeters
            ),

            _ => throw new InvalidOperationException(
                $"Unsupported geometry type " + $"'{geometry.GetType().Name}'."
            ),
        };
    }

    private async Task AppendEventAsync(
        MachineOperation operation,
        MachineOperationEventType eventType,
        MachineOperationStatus? previousStatus,
        MachineOperationStatus? newStatus,
        string? reason,
        Guid? machineAlarmId,
        CancellationToken cancellationToken
    )
    {
        MachineOperationEvent machineOperationEvent = new(
            id: Guid.NewGuid(),
            machineOperationId: operation.Id,
            eventType: eventType,
            occurredAt: DateTimeOffset.UtcNow,
            previousStatus: previousStatus,
            newStatus: newStatus,
            progressPercentage: operation.ProgressPercentage,
            phase: operation.CurrentPhase,
            reason: reason,
            machineAlarmId: machineAlarmId,
            metadata: null
        );

        await _machineOperationEventRepository.AddAsync(machineOperationEvent, cancellationToken);
        await _notificationPublisher.PublishAsync(
            new OperationEventAppendedNotification(
                EventId: machineOperationEvent.Id,
                OperationId: machineOperationEvent.MachineOperationId,
                EventType: machineOperationEvent.EventType,
                OccurredAt: machineOperationEvent.OccurredAt
            ),
            cancellationToken
        );
    }

    private async Task<MachineRuntimeState> GetOrCreateRuntimeStateAsync(
        string machineId,
        CancellationToken cancellationToken,
        MachineOperation? operation = null
    )
    {
        MachineRuntimeState? state = await _machineRuntimeStateRepository.GetByMachineIdAsync(
            machineId,
            cancellationToken
        );

        if (state is not null)
        {
            if (
                operation is not null
                && state.CurrentOperationId is null
                && operation.Status
                    is MachineOperationStatus.Running
                        or MachineOperationStatus.Paused
                        or MachineOperationStatus.Faulted
            )
            {
                int expectedVersion = state.Version;
                SynchronizeRuntimeStateWithOperation(state, operation);
                await _machineRuntimeStateRepository.UpdateAsync(
                    state,
                    expectedVersion,
                    cancellationToken
                );
            }

            return state;
        }

        MachineRuntimeState created =
            operation is not null
            && operation.Status
                is MachineOperationStatus.Running
                    or MachineOperationStatus.Paused
                    or MachineOperationStatus.Faulted
                ? CreateRuntimeStateFromOperation(operation)
                : MachineRuntimeState.CreateAvailable(machineId, DateTimeOffset.UtcNow);

        await _machineRuntimeStateRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private async Task EnsureMachineCanAcceptOperationAsync(
        MachineRuntimeState runtimeState,
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        if (
            runtimeState.Status
            is MachineRuntimeStatus.Faulted
                or MachineRuntimeStatus.Maintenance
                or MachineRuntimeStatus.Offline
        )
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} is {runtimeState.Status} and cannot start operation {operation.Id}."
            );
        }

        if (
            runtimeState.CurrentOperationId is Guid currentOperationId
            && currentOperationId != operation.Id
        )
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} is already assigned to operation {currentOperationId}."
            );
        }

        IReadOnlyCollection<MachineAlarm> alarms =
            await _machineAlarmRepository.GetByMachineIdAsync(
                runtimeState.MachineId,
                activeOnly: true,
                cancellationToken
            );

        if (alarms.Any(MachineAlarmBlockingPolicy.IsBlocking))
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} has active blocking alarms and cannot start operation {operation.Id}."
            );
        }
    }

    private async Task EnsureMachineCanResumeOperationAsync(
        MachineRuntimeState runtimeState,
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        if (
            runtimeState.Status
            is MachineRuntimeStatus.Faulted
                or MachineRuntimeStatus.Maintenance
                or MachineRuntimeStatus.Offline
        )
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} is {runtimeState.Status} and cannot resume operation {operation.Id}."
            );
        }

        if (
            runtimeState.CurrentOperationId is Guid currentOperationId
            && currentOperationId != operation.Id
        )
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} is already assigned to operation {currentOperationId}."
            );
        }

        IReadOnlyCollection<MachineAlarm> alarms =
            await _machineAlarmRepository.GetByMachineIdAsync(
                runtimeState.MachineId,
                activeOnly: true,
                cancellationToken
            );

        if (alarms.Any(MachineAlarmBlockingPolicy.IsBlocking))
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} still has blocking alarms and operation {operation.Id} cannot resume."
            );
        }
    }

    private async Task SaveRuntimeStateAsync(
        MachineRuntimeState runtimeState,
        int expectedVersion,
        CancellationToken cancellationToken
    )
    {
        await _machineRuntimeStateRepository.UpdateAsync(
            runtimeState,
            expectedVersion,
            cancellationToken
        );
    }

    private async Task PublishOperationStatusNotificationAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        await _notificationPublisher.PublishAsync(
            new OperationStatusChangedNotification(
                OperationId: operation.Id,
                Status: operation.Status,
                OccurredAt: DateTimeOffset.UtcNow
            ),
            cancellationToken
        );
    }

    private async Task PublishMachineRuntimeNotificationAsync(
        MachineRuntimeState runtimeState,
        CancellationToken cancellationToken
    )
    {
        await _notificationPublisher.PublishAsync(
            new MachineRuntimeStatusChangedNotification(
                MachineId: runtimeState.MachineId,
                Status: runtimeState.Status,
                CurrentOperationId: runtimeState.CurrentOperationId,
                OccurredAt: DateTimeOffset.UtcNow
            ),
            cancellationToken
        );
    }

    private static MachineAlarmResult ToAlarmResult(MachineAlarm alarm)
    {
        return new MachineAlarmResult(
            Id: alarm.Id,
            MachineId: alarm.MachineId,
            MachineOperationId: alarm.MachineOperationId,
            Code: alarm.Code,
            Severity: alarm.Severity,
            Status: alarm.Status,
            Message: alarm.Message,
            RaisedAt: alarm.RaisedAt,
            AcknowledgedAt: alarm.AcknowledgedAt,
            ResolvedAt: alarm.ResolvedAt,
            ResolutionNotes: alarm.ResolutionNotes
        );
    }

    private static bool CanResume(
        MachineOperation operation,
        MachineRuntimeState runtimeState,
        IReadOnlyCollection<MachineAlarm> activeAlarms
    )
    {
        return operation.Status == MachineOperationStatus.Paused
            && runtimeState.Status is MachineRuntimeStatus.Paused or MachineRuntimeStatus.Available
            && runtimeState.CurrentOperationId == operation.Id
            && !activeAlarms.Any(MachineAlarmBlockingPolicy.IsBlocking);
    }

    private static MachineRuntimeState CreateRuntimeStateFromOperation(MachineOperation operation)
    {
        MachineRuntimeStatus status = operation.Status switch
        {
            MachineOperationStatus.Running => MachineRuntimeStatus.Running,
            MachineOperationStatus.Paused => MachineRuntimeStatus.Paused,
            MachineOperationStatus.Faulted => MachineRuntimeStatus.Faulted,
            _ => MachineRuntimeStatus.Available,
        };

        return MachineRuntimeState.Restore(
            machineId: operation.MachineId,
            status: status,
            currentOperationId: status == MachineRuntimeStatus.Available ? null : operation.Id,
            lastChangedAt: DateTimeOffset.UtcNow,
            failureReason: operation.FailureReason,
            activeAlarmId: null,
            version: 1
        );
    }

    private static void SynchronizeRuntimeStateWithOperation(
        MachineRuntimeState runtimeState,
        MachineOperation operation
    )
    {
        DateTimeOffset changedAt = DateTimeOffset.UtcNow;

        if (operation.Status == MachineOperationStatus.Running)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            return;
        }

        if (operation.Status == MachineOperationStatus.Paused)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            runtimeState.PauseOperation(operation.Id, changedAt);
            return;
        }

        if (operation.Status == MachineOperationStatus.Faulted)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            runtimeState.Fault(
                operation.Id,
                Guid.NewGuid(),
                operation.FailureReason ?? "Faulted",
                changedAt
            );
        }
    }
}
