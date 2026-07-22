using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionSequenceServiceTests
{
    private readonly InMemoryMachineOperationRepository _operationRepository = new();
    private readonly InMemoryWorkpieceRepository _workpieceRepository = new();
    private readonly InMemoryProductionLotRepository _productionLotRepository = new();
    private readonly InMemoryMachineOperationEventRepository _eventRepository;
    private readonly InMemoryMachineAlarmRepository _alarmRepository = new();
    private readonly InMemoryMachineRuntimeStateRepository _runtimeStateRepository = new();
    private readonly BufferedRecordingNotificationPublisher _notificationPublisher = new();
    private readonly BufferedTestProductionTransactionManager _transactionManager;
    private readonly MachineOperationStartCoordinator _operationStartCoordinator;
    private readonly ProductionSequenceService _sequenceService;
    private readonly MachineOperationApplicationService _operationService;
    private readonly MachineAlarmApplicationService _alarmService;

    public ProductionSequenceServiceTests()
    {
        InMemoryProductionCatalog catalog = new();
        _transactionManager = new BufferedTestProductionTransactionManager(_notificationPublisher);
        _eventRepository = new InMemoryMachineOperationEventRepository(
            _operationRepository,
            _workpieceRepository
        );

        _operationStartCoordinator = new MachineOperationStartCoordinator(
            machineOperationRepository: _operationRepository,
            machineOperationEventRepository: _eventRepository,
            machineAlarmRepository: _alarmRepository,
            machineRuntimeStateRepository: _runtimeStateRepository,
            notificationPublisher: _notificationPublisher
        );

        _sequenceService = new ProductionSequenceService(
            _productionLotRepository,
            _workpieceRepository,
            _operationRepository,
            _eventRepository,
            _operationStartCoordinator,
            _transactionManager,
            NullLogger<ProductionSequenceService>.Instance
        );

        _operationService = new MachineOperationApplicationService(
            materialRepository: new InMemoryMaterialRepository(catalog),
            nozzleRepository: new InMemoryNozzleRepository(catalog),
            drawingFileRepository: new InMemoryDrawingFileRepository(catalog),
            machineCapabilitiesRepository: new InMemoryMachineCapabilitiesRepository(catalog),
            workpieceRepository: _workpieceRepository,
            machineOperationRepository: _operationRepository,
            machineOperationEventRepository: _eventRepository,
            machineAlarmRepository: _alarmRepository,
            machineRuntimeStateRepository: _runtimeStateRepository,
            transactionManager: _transactionManager,
            productionSequenceService: _sequenceService,
            operationStartCoordinator: _operationStartCoordinator,
            configurationValidator: new Domain.Technology.LaserCutConfigurationValidator(),
            notificationPublisher: _notificationPublisher,
            logger: NullLogger<MachineOperationApplicationService>.Instance
        );

        _alarmService = new MachineAlarmApplicationService(
            machineAlarmRepository: _alarmRepository,
            machineOperationRepository: _operationRepository,
            machineOperationEventRepository: _eventRepository,
            machineRuntimeStateRepository: _runtimeStateRepository,
            transactionManager: _transactionManager,
            notificationPublisher: _notificationPublisher
        );
    }

    [Fact]
    public async Task StartAsync_PublishesStatusRuntimeAndEventNotifications()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(operation.Id, "Preparing laser"),
            CancellationToken.None
        );

        OperationStatusChangedNotification statusNotification = AssertSinglePublished<
            OperationStatusChangedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();
        OperationEventAppendedNotification eventNotification = AssertSinglePublished<
            OperationEventAppendedNotification
        >();

        Assert.Equal(operation.Id, statusNotification.OperationId);
        Assert.Equal(MachineOperationStatus.Running, statusNotification.Status);

        Assert.Equal(operation.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeNotification.Status);
        Assert.Equal(operation.Id, runtimeNotification.CurrentOperationId);

        Assert.Equal(operation.Id, eventNotification.OperationId);
        Assert.Equal(MachineOperationEventType.Started, eventNotification.EventType);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task UpdateProgressAsync_PublishesProgressNotification()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(operation.Id, "Preparing laser"),
            CancellationToken.None
        );

        _notificationPublisher.ClearPublished();

        await _operationService.UpdateProgressAsync(
            new UpdateMachineOperationProgressCommand(operation.Id, 35, "Laser cutting"),
            CancellationToken.None
        );

        OperationProgressChangedNotification notification = AssertSinglePublished<
            OperationProgressChangedNotification
        >();

        Assert.Equal(operation.Id, notification.OperationId);
        Assert.Equal(35, notification.ProgressPercentage);
        Assert.Equal("Laser cutting", notification.CurrentPhase);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task UpdateProgressAsync_WhenProgressRegresses_ThrowsAndDoesNotPublishNotification()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(operation.Id, "Preparing laser"),
            CancellationToken.None
        );
        await _operationService.UpdateProgressAsync(
            new UpdateMachineOperationProgressCommand(operation.Id, 35, "Laser cutting"),
            CancellationToken.None
        );

        _notificationPublisher.ClearPublished();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _operationService.UpdateProgressAsync(
                new UpdateMachineOperationProgressCommand(operation.Id, 20, "Backtracking"),
                CancellationToken.None
            )
        );

        Assert.True(_operationRepository.TryGetValue(operation.Id, out MachineOperation? stored));
        Assert.NotNull(stored);
        Assert.Equal(35, stored.ProgressPercentage);
        Assert.Equal("Laser cutting", stored.CurrentPhase);
        Assert.Empty(_notificationPublisher.Published);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task UpdateProgressAsync_WhenRuntimeAssignedToDifferentOperation_ThrowsDiagnosticMessage()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        SeedOperation(operation);

        Guid assignedOperationId = Guid.NewGuid();
        MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
            operation.MachineId,
            DateTimeOffset.UtcNow
        );
        runtimeState.StartOperation(assignedOperationId, DateTimeOffset.UtcNow);
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);

        BusinessRuleViolationException exception =
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
                _operationService.UpdateProgressAsync(
                    new UpdateMachineOperationProgressCommand(
                        operation.Id,
                        35,
                        "Laser cutting"
                    ),
                    CancellationToken.None
                )
            );

        Assert.Contains(operation.Id.ToString(), exception.Message);
        Assert.Contains(assignedOperationId.ToString(), exception.Message);
        Assert.Contains("assigned to operation", exception.Message);
        Assert.Empty(_notificationPublisher.Published);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task FaultAsync_PublishesAlarmStatusRuntimeAndEventNotifications()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(operation.Id, "Preparing laser"),
            CancellationToken.None
        );

        _notificationPublisher.ClearPublished();

        await _operationService.FaultAsync(
            new FaultMachineOperationCommand(
                operation.Id,
                "ALARM-001",
                "Gas pressure drop",
                "Gas pressure is below threshold.",
                MachineAlarmSeverity.Warning
            ),
            CancellationToken.None
        );

        MachineAlarmRaisedNotification alarmNotification = AssertSinglePublished<
            MachineAlarmRaisedNotification
        >();
        OperationStatusChangedNotification statusNotification = AssertSinglePublished<
            OperationStatusChangedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();
        OperationEventAppendedNotification eventNotification = AssertSinglePublished<
            OperationEventAppendedNotification
        >();

        Assert.Equal(operation.MachineId, alarmNotification.MachineId);
        Assert.Equal(operation.Id, alarmNotification.OperationId);

        Assert.Equal(operation.Id, statusNotification.OperationId);
        Assert.Equal(MachineOperationStatus.Faulted, statusNotification.Status);

        Assert.Equal(operation.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Faulted, runtimeNotification.Status);
        Assert.Equal(operation.Id, runtimeNotification.CurrentOperationId);

        Assert.Equal(operation.Id, eventNotification.OperationId);
        Assert.Equal(MachineOperationEventType.Faulted, eventNotification.EventType);
    }

    [Fact]
    public async Task AcknowledgeAndResolveAlarm_PublishExpectedNotifications()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(operation.Id, "Preparing laser"),
            CancellationToken.None
        );
        await _operationService.FaultAsync(
            new FaultMachineOperationCommand(
                operation.Id,
                "ALARM-001",
                "Gas pressure drop",
                "Gas pressure is below threshold.",
                MachineAlarmSeverity.Warning
            ),
            CancellationToken.None
        );

        MachineAlarm alarm = Assert.Single(
            await _alarmRepository.GetByOperationIdAsync(operation.Id, CancellationToken.None)
        );

        _notificationPublisher.ClearPublished();

        await _alarmService.AcknowledgeAsync(
            new AcknowledgeMachineAlarmCommand(alarm.Id),
            CancellationToken.None
        );

        MachineAlarmAcknowledgedNotification acknowledgedNotification = AssertSinglePublished<
            MachineAlarmAcknowledgedNotification
        >();
        Assert.Equal(alarm.Id, acknowledgedNotification.AlarmId);
        Assert.Equal(alarm.MachineId, acknowledgedNotification.MachineId);
        Assert.Empty(_notificationPublisher.Pending);

        _notificationPublisher.ClearPublished();

        await _alarmService.ResolveAsync(
            new ResolveMachineAlarmCommand(alarm.Id, "Pressure stabilized"),
            CancellationToken.None
        );

        MachineAlarmResolvedNotification resolvedNotification = AssertSinglePublished<
            MachineAlarmResolvedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();
        OperationEventAppendedNotification eventNotification = AssertSinglePublished<
            OperationEventAppendedNotification
        >();

        Assert.Equal(alarm.Id, resolvedNotification.AlarmId);
        Assert.Equal(alarm.MachineId, resolvedNotification.MachineId);

        Assert.Equal(operation.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Paused, runtimeNotification.Status);
        Assert.Equal(operation.Id, runtimeNotification.CurrentOperationId);

        Assert.Equal(operation.Id, eventNotification.OperationId);
        Assert.Equal(MachineOperationEventType.Recovered, eventNotification.EventType);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task ResolveAsync_WhenMachineLevelAlarmClearsLastBlockingAlarm_PublishesRuntimeNotification()
    {
        DateTimeOffset raisedAt = DateTimeOffset.UtcNow;
        MachineAlarm alarm = new(
            id: Guid.NewGuid(),
            machineId: "M-001",
            machineOperationId: null,
            code: "MACHINE-FAULT",
            severity: MachineAlarmSeverity.Critical,
            message: "Machine fault.",
            raisedAt: raisedAt
        );
        MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
            alarm.MachineId,
            raisedAt
        );
        runtimeState.Fault(
            operationId: null,
            alarmId: alarm.Id,
            failureReason: alarm.Message,
            changedAt: raisedAt
        );
        await _alarmRepository.AddAsync(alarm, CancellationToken.None);
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);
        _notificationPublisher.ClearPublished();

        await _alarmService.ResolveAsync(
            new ResolveMachineAlarmCommand(alarm.Id, "Fault cleared"),
            CancellationToken.None
        );

        MachineAlarmResolvedNotification resolvedNotification = AssertSinglePublished<
            MachineAlarmResolvedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();
        MachineRuntimeState? storedRuntimeState =
            await _runtimeStateRepository.GetByMachineIdAsync(alarm.MachineId, CancellationToken.None);

        Assert.Equal(alarm.Id, resolvedNotification.AlarmId);
        Assert.Equal(alarm.MachineId, resolvedNotification.MachineId);

        Assert.Equal(alarm.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Available, runtimeNotification.Status);
        Assert.Null(runtimeNotification.CurrentOperationId);

        Assert.NotNull(storedRuntimeState);
        Assert.Equal(MachineRuntimeStatus.Available, storedRuntimeState.Status);
        Assert.Null(storedRuntimeState.CurrentOperationId);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task ResolveAsync_WhenAnotherBlockingMachineLevelAlarmRemains_DoesNotPublishRuntimeNotification()
    {
        DateTimeOffset raisedAt = DateTimeOffset.UtcNow;
        MachineAlarm alarmToResolve = new(
            id: Guid.NewGuid(),
            machineId: "M-001",
            machineOperationId: null,
            code: "MACHINE-FAULT-1",
            severity: MachineAlarmSeverity.Critical,
            message: "First machine fault.",
            raisedAt: raisedAt
        );
        MachineAlarm remainingBlockingAlarm = new(
            id: Guid.NewGuid(),
            machineId: "M-001",
            machineOperationId: null,
            code: "MACHINE-FAULT-2",
            severity: MachineAlarmSeverity.Critical,
            message: "Second machine fault.",
            raisedAt: raisedAt.AddSeconds(1)
        );
        MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
            alarmToResolve.MachineId,
            raisedAt
        );
        runtimeState.Fault(
            operationId: null,
            alarmId: alarmToResolve.Id,
            failureReason: alarmToResolve.Message,
            changedAt: raisedAt
        );
        await _alarmRepository.AddAsync(alarmToResolve, CancellationToken.None);
        await _alarmRepository.AddAsync(remainingBlockingAlarm, CancellationToken.None);
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);
        _notificationPublisher.ClearPublished();

        await _alarmService.ResolveAsync(
            new ResolveMachineAlarmCommand(alarmToResolve.Id, "First fault cleared"),
            CancellationToken.None
        );

        MachineAlarmResolvedNotification resolvedNotification = AssertSinglePublished<
            MachineAlarmResolvedNotification
        >();
        MachineRuntimeState? storedRuntimeState =
            await _runtimeStateRepository.GetByMachineIdAsync(
                alarmToResolve.MachineId,
                CancellationToken.None
            );

        Assert.Equal(alarmToResolve.Id, resolvedNotification.AlarmId);
        Assert.DoesNotContain(
            _notificationPublisher.Published,
            notification => notification is MachineRuntimeStatusChangedNotification
        );

        Assert.NotNull(storedRuntimeState);
        Assert.Equal(MachineRuntimeStatus.Faulted, storedRuntimeState.Status);
        Assert.Equal(alarmToResolve.Id, storedRuntimeState.ActiveAlarmId);
        Assert.Empty(_notificationPublisher.Pending);
    }

    private TNotification AssertSinglePublished<TNotification>()
        where TNotification : ProductionNotification
    {
        return Assert.Single(_notificationPublisher.Published.OfType<TNotification>());
    }

    [Fact]
    public async Task StartSingleOperation_WhenPreviousOperationIsNotCompleted_Throws()
    {
        (ProductionLot lot, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _operationService.StartAsync(
                new StartMachineOperationCommand(second.Id, "Preparing laser"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task CompleteSingleStartedOperation_DoesNotStartNextQueuedOperation()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(first.Id, "Preparing laser"),
            CancellationToken.None
        );

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );
        Workpiece? storedWorkpiece = await _workpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedSecond);
        Assert.NotNull(storedWorkpiece);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
        Assert.False(storedWorkpiece.IsSequenceActive);
    }

    [Fact]
    public async Task StartWorkpiece_StartsOnlyFirstOperation()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing laser",
            startFromSequenceNumber: null,
            CancellationToken.None
        );

        MachineOperation? storedFirst = await _operationRepository.GetByIdAsync(
            first.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.Equal(MachineOperationStatus.Running, storedFirst.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
    }

    [Fact]
    public async Task StartWorkpiece_StartsFirstOperationAndAssignsMachineRuntime()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing laser",
            startFromSequenceNumber: null,
            CancellationToken.None
        );

        MachineRuntimeState? runtimeState = await _runtimeStateRepository.GetByMachineIdAsync(
            first.MachineId,
            CancellationToken.None
        );
        OperationStatusChangedNotification statusNotification = AssertSinglePublished<
            OperationStatusChangedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();
        OperationEventAppendedNotification eventNotification = AssertSinglePublished<
            OperationEventAppendedNotification
        >();

        Assert.NotNull(runtimeState);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeState.Status);
        Assert.Equal(first.Id, runtimeState.CurrentOperationId);

        Assert.Equal(first.Id, statusNotification.OperationId);
        Assert.Equal(MachineOperationStatus.Running, statusNotification.Status);

        Assert.Equal(first.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeNotification.Status);
        Assert.Equal(first.Id, runtimeNotification.CurrentOperationId);

        Assert.Equal(first.Id, eventNotification.OperationId);
        Assert.Equal(MachineOperationEventType.Started, eventNotification.EventType);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task CompletingOperationInActiveSequence_StartsNextSameWorkpieceOperation()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing laser",
            startFromSequenceNumber: null,
            CancellationToken.None
        );

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedSecond);
        Assert.Equal(MachineOperationStatus.Running, storedSecond.Status);
    }

    [Fact]
    public async Task HandleOperationCompleted_StartsNextOperationAndReassignsMachineRuntime()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing first",
            startFromSequenceNumber: null,
            CancellationToken.None
        );
        _notificationPublisher.ClearPublished();

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        MachineRuntimeState? runtimeState = await _runtimeStateRepository.GetByMachineIdAsync(
            first.MachineId,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );
        OperationStatusChangedNotification startedStatusNotification = Assert.Single(
            _notificationPublisher.Published.OfType<OperationStatusChangedNotification>(),
            notification =>
                notification.OperationId == second.Id
                && notification.Status == MachineOperationStatus.Running
        );
        MachineRuntimeStatusChangedNotification runtimeNotification = Assert.Single(
            _notificationPublisher.Published.OfType<MachineRuntimeStatusChangedNotification>(),
            notification =>
                notification.CurrentOperationId == second.Id
                && notification.Status == MachineRuntimeStatus.Running
        );

        Assert.NotNull(storedSecond);
        Assert.Equal(MachineOperationStatus.Running, storedSecond.Status);
        Assert.NotNull(runtimeState);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeState.Status);
        Assert.Equal(second.Id, runtimeState.CurrentOperationId);

        Assert.Equal(second.Id, startedStatusNotification.OperationId);
        Assert.Equal(first.MachineId, runtimeNotification.MachineId);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task StartWorkpiece_WhenMachineAlreadyAssignedToDifferentOperation_FailsWithoutPartialState()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);

        Guid assignedOperationId = Guid.NewGuid();
        MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
            operation.MachineId,
            DateTimeOffset.UtcNow
        );
        runtimeState.StartOperation(assignedOperationId, DateTimeOffset.UtcNow);
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _sequenceService.StartWorkpieceAsync(
                workpiece.Id,
                "Preparing laser",
                startFromSequenceNumber: null,
                CancellationToken.None
            )
        );

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );
        MachineRuntimeState? storedRuntimeState = await _runtimeStateRepository.GetByMachineIdAsync(
            operation.MachineId,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.NotNull(storedRuntimeState);
        Assert.Equal(MachineOperationStatus.Queued, storedOperation.Status);
        Assert.Equal(MachineRuntimeStatus.Running, storedRuntimeState.Status);
        Assert.Equal(assignedOperationId, storedRuntimeState.CurrentOperationId);
        Assert.Empty(_notificationPublisher.Published);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task StartAsync_WhenRuntimeRunningWithoutCurrentOperation_Throws()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);
        MachineRuntimeState runtimeState = MachineRuntimeState.Restore(
            machineId: operation.MachineId,
            status: MachineRuntimeStatus.Running,
            currentOperationId: null,
            lastChangedAt: DateTimeOffset.UtcNow,
            failureReason: null,
            activeAlarmId: null,
            version: 1
        );
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);

        BusinessRuleViolationException exception =
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
                _operationService.StartAsync(
                    new StartMachineOperationCommand(operation.Id, "Preparing laser"),
                    CancellationToken.None
                )
            );

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Queued, storedOperation.Status);
        Assert.Contains("Running without a current operation assignment", exception.Message);
        Assert.Empty(_notificationPublisher.Published);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Theory]
    [InlineData(MachineRuntimeStatus.Faulted)]
    [InlineData(MachineRuntimeStatus.Maintenance)]
    [InlineData(MachineRuntimeStatus.Offline)]
    public async Task StartAsync_WhenRuntimeUnavailable_Throws(MachineRuntimeStatus status)
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        SeedOperation(operation);
        MachineRuntimeState runtimeState = MachineRuntimeState.Restore(
            machineId: operation.MachineId,
            status: status,
            currentOperationId: null,
            lastChangedAt: DateTimeOffset.UtcNow,
            failureReason: "Unavailable",
            activeAlarmId: null,
            version: 1
        );
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);

        BusinessRuleViolationException exception =
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
                _operationService.StartAsync(
                    new StartMachineOperationCommand(operation.Id, "Preparing laser"),
                    CancellationToken.None
                )
            );

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Queued, storedOperation.Status);
        Assert.Contains(status.ToString(), exception.Message);
        Assert.Empty(_notificationPublisher.Published);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task StartWorkpieceFromSequence_SkipsPreviousQueuedOperationsAndRegistersEvents()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        MachineOperation third = CreateQueuedOperation(workpiece.Id, 3);
        MachineOperation fourth = CreateQueuedOperation(workpiece.Id, 4);
        SeedOperation(first);
        SeedOperation(second);
        SeedOperation(third);
        SeedOperation(fourth);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing laser",
            startFromSequenceNumber: 3,
            CancellationToken.None
        );

        MachineOperation? storedFirst = await _operationRepository.GetByIdAsync(
            first.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );
        MachineOperation? storedThird = await _operationRepository.GetByIdAsync(
            third.Id,
            CancellationToken.None
        );
        IReadOnlyCollection<MachineOperationEvent> events = await _eventRepository
            .GetByWorkpieceIdAsync(workpiece.Id, CancellationToken.None);

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(storedThird);
        Assert.Equal(MachineOperationStatus.Skipped, storedFirst.Status);
        Assert.Equal(MachineOperationStatus.Skipped, storedSecond.Status);
        Assert.Equal(MachineOperationStatus.Running, storedThird.Status);
        Assert.Equal(2, events.Count(item => item.EventType == MachineOperationEventType.Skipped));
        Assert.Contains(
            events,
            item =>
                item.MachineOperationId == third.Id
                && item.EventType == MachineOperationEventType.Started
        );
    }

    [Fact]
    public async Task StartProductionLotFromWorkpieceSequence_StartsOnlySelectedWorkpiece()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-001",
            plannedQuantity: 3,
            createdAt: DateTimeOffset.UtcNow
        );
        await _productionLotRepository.AddAsync(lot, CancellationToken.None);

        Workpiece firstWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece secondWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 2,
            code: "WP-002",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece thirdWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 3,
            code: "WP-003",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await _workpieceRepository.AddAsync(firstWorkpiece, CancellationToken.None);
        await _workpieceRepository.AddAsync(secondWorkpiece, CancellationToken.None);
        await _workpieceRepository.AddAsync(thirdWorkpiece, CancellationToken.None);

        MachineOperation firstOperation = CreateQueuedOperation(firstWorkpiece.Id, 1);
        MachineOperation secondOperation = CreateQueuedOperation(secondWorkpiece.Id, 1);
        MachineOperation thirdOperation = CreateQueuedOperation(thirdWorkpiece.Id, 1);
        SeedOperation(firstOperation);
        SeedOperation(secondOperation);
        SeedOperation(thirdOperation);

        await _sequenceService.StartProductionLotAsync(
            lot.Id,
            "Preparing laser",
            startFromWorkpieceSequenceNumber: 3,
            CancellationToken.None
        );

        Workpiece? storedFirst = await _workpieceRepository.GetByIdAsync(
            firstWorkpiece.Id,
            CancellationToken.None
        );
        Workpiece? storedSecond = await _workpieceRepository.GetByIdAsync(
            secondWorkpiece.Id,
            CancellationToken.None
        );
        Workpiece? storedThird = await _workpieceRepository.GetByIdAsync(
            thirdWorkpiece.Id,
            CancellationToken.None
        );
        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );
        MachineOperation? storedFirstOperation = await _operationRepository.GetByIdAsync(
            firstOperation.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecondOperation = await _operationRepository.GetByIdAsync(
            secondOperation.Id,
            CancellationToken.None
        );
        MachineOperation? storedThirdOperation = await _operationRepository.GetByIdAsync(
            thirdOperation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedLot);
        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(storedThird);
        Assert.NotNull(storedFirstOperation);
        Assert.NotNull(storedSecondOperation);
        Assert.NotNull(storedThirdOperation);
        Assert.Equal(ProductionLotExecutionMode.LotSequence, storedLot.ExecutionMode);
        Assert.Equal(WorkpieceStatus.Pending, storedFirst.Status);
        Assert.Equal(WorkpieceStatus.Pending, storedSecond.Status);
        Assert.Equal(WorkpieceStatus.Running, storedThird.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedFirstOperation.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedSecondOperation.Status);
        Assert.Equal(MachineOperationStatus.Running, storedThirdOperation.Status);
    }

    [Fact]
    public async Task StartProductionLot_StartsOnlyInitialWorkpiece()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-001",
            plannedQuantity: 2,
            createdAt: DateTimeOffset.UtcNow
        );
        await _productionLotRepository.AddAsync(lot, CancellationToken.None);

        Workpiece firstWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece secondWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 2,
            code: "WP-002",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await _workpieceRepository.AddAsync(firstWorkpiece, CancellationToken.None);
        await _workpieceRepository.AddAsync(secondWorkpiece, CancellationToken.None);

        SeedOperation(CreateQueuedOperation(firstWorkpiece.Id, 1, machineId: "M-001"));
        SeedOperation(CreateQueuedOperation(secondWorkpiece.Id, 1, machineId: "M-002"));

        await _sequenceService.StartProductionLotAsync(
            lot.Id,
            "Preparing laser",
            startFromWorkpieceSequenceNumber: null,
            CancellationToken.None
        );

        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );
        Workpiece? storedFirst = await _workpieceRepository.GetByIdAsync(
            firstWorkpiece.Id,
            CancellationToken.None
        );
        Workpiece? storedSecond = await _workpieceRepository.GetByIdAsync(
            secondWorkpiece.Id,
            CancellationToken.None
        );
        IReadOnlyCollection<MachineOperation> firstOperations =
            await _operationRepository.GetOrderedByWorkpieceIdAsync(
                firstWorkpiece.Id,
                CancellationToken.None
            );
        IReadOnlyCollection<MachineOperation> secondOperations =
            await _operationRepository.GetOrderedByWorkpieceIdAsync(
                secondWorkpiece.Id,
                CancellationToken.None
            );

        Assert.NotNull(storedLot);
        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.Equal(ProductionLotExecutionMode.LotSequence, storedLot.ExecutionMode);
        Assert.True(storedFirst.IsSequenceActive);
        Assert.False(storedSecond.IsSequenceActive);
        Assert.Equal(WorkpieceStatus.Running, storedFirst.Status);
        Assert.Equal(WorkpieceStatus.Pending, storedSecond.Status);
        Assert.Single(
            firstOperations,
            operation => operation.Status == MachineOperationStatus.Running
        );
        Assert.All(
            secondOperations,
            operation => Assert.Equal(MachineOperationStatus.Queued, operation.Status)
        );
    }

    [Fact]
    public async Task StartProductionLot_StartsFirstOperationAndAssignsMachineRuntime()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece workpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await _productionLotRepository.AddAsync(lot, CancellationToken.None);
        await _workpieceRepository.AddAsync(workpiece, CancellationToken.None);

        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartProductionLotAsync(
            lot.Id,
            "Preparing laser",
            startFromWorkpieceSequenceNumber: null,
            CancellationToken.None
        );

        MachineRuntimeState? runtimeState = await _runtimeStateRepository.GetByMachineIdAsync(
            first.MachineId,
            CancellationToken.None
        );
        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );
        OperationStatusChangedNotification statusNotification = AssertSinglePublished<
            OperationStatusChangedNotification
        >();
        MachineRuntimeStatusChangedNotification runtimeNotification = AssertSinglePublished<
            MachineRuntimeStatusChangedNotification
        >();

        Assert.NotNull(storedLot);
        Assert.Equal(ProductionLotExecutionMode.LotSequence, storedLot.ExecutionMode);
        Assert.NotNull(runtimeState);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeState.Status);
        Assert.Equal(first.Id, runtimeState.CurrentOperationId);

        Assert.Equal(first.Id, statusNotification.OperationId);
        Assert.Equal(MachineOperationStatus.Running, statusNotification.Status);

        Assert.Equal(first.MachineId, runtimeNotification.MachineId);
        Assert.Equal(MachineRuntimeStatus.Running, runtimeNotification.Status);
        Assert.Equal(first.Id, runtimeNotification.CurrentOperationId);
        Assert.Empty(_notificationPublisher.Pending);
    }

    [Fact]
    public async Task FailedOperation_BlocksSequenceOfWorkpiece()
    {
        (ProductionLot lot, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateRunningOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        lot.StartLotSequence(DateTimeOffset.UtcNow);
        workpiece.StartSequence(DateTimeOffset.UtcNow);
        await _productionLotRepository.UpdateAsync(lot, CancellationToken.None);
        await _workpieceRepository.UpdateAsync(workpiece, CancellationToken.None);
        SeedOperation(first);
        SeedOperation(second);

        await _operationService.FailAsync(
            new FailMachineOperationCommand(first.Id, "Laser unavailable"),
            CancellationToken.None
        );

        Workpiece? storedWorkpiece = await _workpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );
        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedWorkpiece);
        Assert.NotNull(storedLot);
        Assert.NotNull(storedSecond);
        Assert.Equal(WorkpieceStatus.Failed, storedWorkpiece.Status);
        Assert.False(storedWorkpiece.IsSequenceActive);
        Assert.Equal(ProductionLotExecutionMode.None, storedLot.ExecutionMode);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
    }

    [Fact]
    public async Task CompletingLastOperation_WithLotSequence_StartsNextWorkpieceOnly()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-SEQ-001",
            plannedQuantity: 2,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece firstWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece secondWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 2,
            code: "WP-002",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        lot.StartLotSequence(DateTimeOffset.UtcNow);
        firstWorkpiece.StartSequence(DateTimeOffset.UtcNow);
        await _productionLotRepository.AddAsync(lot, CancellationToken.None);
        await _workpieceRepository.AddAsync(firstWorkpiece, CancellationToken.None);
        await _workpieceRepository.AddAsync(secondWorkpiece, CancellationToken.None);

        MachineOperation firstOperation = CreateRunningOperation(firstWorkpiece.Id, 1);
        MachineOperation secondWorkpieceFirstOperation = CreateQueuedOperation(secondWorkpiece.Id, 1);
        MachineOperation secondWorkpieceSecondOperation = CreateQueuedOperation(secondWorkpiece.Id, 2);
        SeedOperation(firstOperation);
        SeedOperation(secondWorkpieceFirstOperation);
        SeedOperation(secondWorkpieceSecondOperation);

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(firstOperation.Id),
            CancellationToken.None
        );

        Workpiece? storedFirstWorkpiece = await _workpieceRepository.GetByIdAsync(
            firstWorkpiece.Id,
            CancellationToken.None
        );
        Workpiece? storedSecondWorkpiece = await _workpieceRepository.GetByIdAsync(
            secondWorkpiece.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecondWorkpieceFirstOperation =
            await _operationRepository.GetByIdAsync(
                secondWorkpieceFirstOperation.Id,
                CancellationToken.None
            );
        MachineOperation? storedSecondWorkpieceSecondOperation =
            await _operationRepository.GetByIdAsync(
                secondWorkpieceSecondOperation.Id,
                CancellationToken.None
            );
        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedFirstWorkpiece);
        Assert.NotNull(storedSecondWorkpiece);
        Assert.NotNull(storedSecondWorkpieceFirstOperation);
        Assert.NotNull(storedSecondWorkpieceSecondOperation);
        Assert.NotNull(storedLot);
        Assert.Equal(WorkpieceStatus.Completed, storedFirstWorkpiece.Status);
        Assert.Equal(WorkpieceStatus.Running, storedSecondWorkpiece.Status);
        Assert.False(storedFirstWorkpiece.IsSequenceActive);
        Assert.True(storedSecondWorkpiece.IsSequenceActive);
        Assert.Equal(MachineOperationStatus.Running, storedSecondWorkpieceFirstOperation.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedSecondWorkpieceSecondOperation.Status);
        Assert.Equal(ProductionLotExecutionMode.LotSequence, storedLot.ExecutionMode);
        Assert.Single(
            _notificationPublisher.Published.OfType<OperationStatusChangedNotification>(),
            notification =>
                notification.OperationId == secondWorkpieceFirstOperation.Id
                && notification.Status == MachineOperationStatus.Running
        );
        Assert.Single(
            _notificationPublisher.Published.OfType<MachineRuntimeStatusChangedNotification>(),
            notification =>
                notification.CurrentOperationId == secondWorkpieceFirstOperation.Id
                && notification.Status == MachineRuntimeStatus.Running
        );
    }

    [Fact]
    public async Task CompletingLastOperation_WithManualWorkpieceSequence_DoesNotStartNextWorkpiece()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-MANUAL-001",
            plannedQuantity: 2,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece firstWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece secondWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 2,
            code: "WP-002",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        lot.StartManual(DateTimeOffset.UtcNow);
        firstWorkpiece.StartSequence(DateTimeOffset.UtcNow);
        await _productionLotRepository.AddAsync(lot, CancellationToken.None);
        await _workpieceRepository.AddAsync(firstWorkpiece, CancellationToken.None);
        await _workpieceRepository.AddAsync(secondWorkpiece, CancellationToken.None);

        MachineOperation firstOperation = CreateRunningOperation(firstWorkpiece.Id, 1);
        MachineOperation secondWorkpieceFirstOperation = CreateQueuedOperation(secondWorkpiece.Id, 1);
        SeedOperation(firstOperation);
        SeedOperation(secondWorkpieceFirstOperation);

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(firstOperation.Id),
            CancellationToken.None
        );

        Workpiece? storedFirstWorkpiece = await _workpieceRepository.GetByIdAsync(
            firstWorkpiece.Id,
            CancellationToken.None
        );
        Workpiece? storedSecondWorkpiece = await _workpieceRepository.GetByIdAsync(
            secondWorkpiece.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecondWorkpieceFirstOperation =
            await _operationRepository.GetByIdAsync(
                secondWorkpieceFirstOperation.Id,
                CancellationToken.None
            );

        Assert.NotNull(storedFirstWorkpiece);
        Assert.NotNull(storedSecondWorkpiece);
        Assert.NotNull(storedSecondWorkpieceFirstOperation);
        Assert.Equal(WorkpieceStatus.Completed, storedFirstWorkpiece.Status);
        Assert.Equal(WorkpieceStatus.Pending, storedSecondWorkpiece.Status);
        Assert.False(storedSecondWorkpiece.IsSequenceActive);
        Assert.Equal(MachineOperationStatus.Queued, storedSecondWorkpieceFirstOperation.Status);
    }

    [Fact]
    public async Task CompletingRecoveredOperation_WithActiveSequence_StartsNextQueuedOperation()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _sequenceService.StartWorkpieceAsync(
            workpiece.Id,
            "Preparing laser",
            startFromSequenceNumber: null,
            CancellationToken.None
        );

        await _operationService.FaultAsync(
            new FaultMachineOperationCommand(
                first.Id,
                "Assist gas pressure drop",
                "FAULT-001",
                "Gas pressure below threshold",
                MachineAlarmSeverity.Warning
            ),
            CancellationToken.None
        );

        MachineAlarm alarm = (await _alarmRepository.GetByOperationIdAsync(
            first.Id,
            CancellationToken.None
        )).Single();

        await _alarmService.ResolveAsync(
            new ResolveMachineAlarmCommand(alarm.Id, "Pressure stabilized"),
            CancellationToken.None
        );

        await _operationService.ResumeAsync(
            new ResumeMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        Workpiece? storedWorkpiece = await _workpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedWorkpiece);
        Assert.NotNull(storedSecond);
        Assert.True(storedWorkpiece.IsSequenceActive);
        Assert.Equal(MachineOperationStatus.Running, storedSecond.Status);
    }

    [Fact]
    public async Task CompletingRecoveredOperation_WithPointStart_KeepsNextQueuedOperationQueued()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        SeedOperation(first);
        SeedOperation(second);

        await _operationService.StartAsync(
            new StartMachineOperationCommand(first.Id, "Preparing laser"),
            CancellationToken.None
        );

        await _operationService.FaultAsync(
            new FaultMachineOperationCommand(
                first.Id,
                "Assist gas pressure drop",
                "FAULT-002",
                "Gas pressure below threshold",
                MachineAlarmSeverity.Warning
            ),
            CancellationToken.None
        );

        MachineAlarm alarm = (await _alarmRepository.GetByOperationIdAsync(
            first.Id,
            CancellationToken.None
        )).Single();

        await _alarmService.ResolveAsync(
            new ResolveMachineAlarmCommand(alarm.Id, "Pressure stabilized"),
            CancellationToken.None
        );

        await _operationService.ResumeAsync(
            new ResumeMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(first.Id),
            CancellationToken.None
        );

        Workpiece? storedWorkpiece = await _workpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedWorkpiece);
        Assert.NotNull(storedSecond);
        Assert.False(storedWorkpiece.IsSequenceActive);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
    }

    [Fact]
    public async Task CompletingLastOperation_CompletesWorkpieceAndLot()
    {
        (ProductionLot lot, Workpiece workpiece) = await SeedHierarchyAsync();
        workpiece.StartSequence(DateTimeOffset.UtcNow);
        lot.Start(DateTimeOffset.UtcNow);
        await _workpieceRepository.UpdateAsync(workpiece, CancellationToken.None);
        await _productionLotRepository.UpdateAsync(lot, CancellationToken.None);

        MachineOperation operation = CreateRunningOperation(workpiece.Id, 1);
        SeedOperation(operation);

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(operation.Id),
            CancellationToken.None
        );

        Workpiece? storedWorkpiece = await _workpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );
        ProductionLot? storedLot = await _productionLotRepository.GetByIdAsync(
            lot.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedWorkpiece);
        Assert.NotNull(storedLot);
        Assert.Equal(WorkpieceStatus.Completed, storedWorkpiece.Status);
        Assert.Equal(ProductionLotStatus.Completed, storedLot.Status);
    }

    private async Task<(ProductionLot Lot, Workpiece Workpiece)> SeedHierarchyAsync()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: $"LOT-{Guid.NewGuid():N}",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece workpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: $"WP-{Guid.NewGuid():N}",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await _productionLotRepository.AddAsync(lot, CancellationToken.None);
        await _workpieceRepository.AddAsync(workpiece, CancellationToken.None);

        return (lot, workpiece);
    }

    private void SeedOperation(MachineOperation operation)
    {
        _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        ).GetAwaiter().GetResult();
    }

    private static MachineOperation CreateQueuedOperation(
        Guid workpieceId,
        int sequenceNumber,
        string machineId = "M-001"
    )
    {
        return new MachineOperation(
            id: Guid.NewGuid(),
            workpieceId: workpieceId,
            sequenceNumber: sequenceNumber,
            machineId: machineId,
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(sequenceNumber)
        );
    }

    private static MachineOperation CreateRunningOperation(Guid workpieceId, int sequenceNumber)
    {
        MachineOperation operation = CreateQueuedOperation(workpieceId, sequenceNumber);
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        return operation;
    }

    private static Domain.Technology.LaserCutConfiguration CreateConfiguration(Guid operationId)
    {
        return new Domain.Technology.LaserCutConfiguration(
            id: Guid.NewGuid(),
            operationId: operationId,
            materialId: InMemoryProductionData.StainlessSteel304MaterialId,
            nozzleId: InMemoryProductionData.Nozzle12Id,
            drawingFileId: InMemoryProductionData.TubeDrawingId,
            geometry: new Domain.Technology.TubeGeometry(80m, 3m, 6000m),
            laserPowerWatts: 2500m,
            cuttingSpeedMillimetersPerMinute: 1200m,
            assistGas: Domain.Technology.AssistGasType.Nitrogen,
            gasPressureBar: 15m,
            focalOffsetMillimeters: -0.5m,
            numberOfPasses: 1,
            createdAt: DateTimeOffset.UtcNow
        );
    }

    private sealed class BufferedTestProductionTransactionManager : IProductionTransactionManager
    {
        private readonly BufferedRecordingNotificationPublisher _notificationPublisher;

        public BufferedTestProductionTransactionManager(
            BufferedRecordingNotificationPublisher notificationPublisher
        )
        {
            _notificationPublisher = notificationPublisher;
        }

        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken
        )
        {
            return ExecuteInternalAsync(operation, cancellationToken);
        }

        private async Task ExecuteInternalAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await operation(cancellationToken);
                _notificationPublisher.Published.AddRange(_notificationPublisher.GetPending());
            }
            finally
            {
                _notificationPublisher.Clear();
            }
        }
    }

    private sealed class BufferedRecordingNotificationPublisher
        : IProductionNotificationCollector
    {
        public List<ProductionNotification> Pending { get; } = [];

        public List<ProductionNotification> Published { get; } = [];

        public Task PublishAsync(
            ProductionNotification notification,
            CancellationToken cancellationToken
        )
        {
            Pending.Add(notification);
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<ProductionNotification> GetPending()
        {
            return Pending.ToArray();
        }

        public void Clear()
        {
            Pending.Clear();
        }

        public void ClearPublished()
        {
            Pending.Clear();
            Published.Clear();
        }
    }

    private sealed class InMemoryWorkpieceRepository : IWorkpieceRepository
    {
        private readonly Dictionary<Guid, Workpiece> _items = [];

        public Task<Workpiece?> GetByIdAsync(Guid workpieceId, CancellationToken cancellationToken)
        {
            _items.TryGetValue(workpieceId, out Workpiece? workpiece);
            return Task.FromResult(workpiece);
        }

        public Task<IReadOnlyCollection<Workpiece>> GetByProductionLotIdAsync(
            Guid productionLotId,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyCollection<Workpiece>>(
                _items.Values.Where(item => item.ProductionLotId == productionLotId).ToArray()
            );
        }

        public Task AddAsync(Workpiece workpiece, CancellationToken cancellationToken)
        {
            _items.Add(workpiece.Id, workpiece);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Workpiece workpiece, CancellationToken cancellationToken)
        {
            _items[workpiece.Id] = workpiece;
            return Task.CompletedTask;
        }

        public bool TryGetValue(Guid workpieceId, out Workpiece? workpiece)
        {
            bool found = _items.TryGetValue(workpieceId, out Workpiece? storedWorkpiece);
            workpiece = storedWorkpiece;
            return found;
        }
    }

    private sealed class InMemoryProductionLotRepository : IProductionLotRepository
    {
        private readonly Dictionary<Guid, ProductionLot> _items = [];

        public Task<ProductionLot?> GetByIdAsync(
            Guid productionLotId,
            CancellationToken cancellationToken
        )
        {
            _items.TryGetValue(productionLotId, out ProductionLot? lot);
            return Task.FromResult(lot);
        }

        public Task AddAsync(ProductionLot productionLot, CancellationToken cancellationToken)
        {
            _items.Add(productionLot.Id, productionLot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductionLot productionLot, CancellationToken cancellationToken)
        {
            _items[productionLot.Id] = productionLot;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryMachineOperationEventRepository
        : IMachineOperationEventRepository
    {
        private readonly List<MachineOperationEvent> _items = [];
        private readonly InMemoryMachineOperationRepository _operationRepository;
        private readonly InMemoryWorkpieceRepository _workpieceRepository;

        public InMemoryMachineOperationEventRepository(
            InMemoryMachineOperationRepository operationRepository,
            InMemoryWorkpieceRepository workpieceRepository
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
                _items.Where(item => item.MachineOperationId == operationId).ToArray()
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
    }

    private sealed class InMemoryMachineAlarmRepository : IMachineAlarmRepository
    {
        private readonly Dictionary<Guid, MachineAlarm> _alarms = [];

        public Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken)
            => Task.FromResult(
                _alarms.TryGetValue(alarmId, out MachineAlarm? alarm) ? alarm : null
            );

        public Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
            string machineId,
            bool activeOnly,
            CancellationToken cancellationToken
        )
        {
            IEnumerable<MachineAlarm> query = _alarms.Values.Where(item => item.MachineId == machineId);

            if (activeOnly)
            {
                query = query.Where(item => item.Status != MachineAlarmStatus.Resolved);
            }

            return Task.FromResult<IReadOnlyCollection<MachineAlarm>>(query.ToArray());
        }

        public Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
            Guid operationId,
            CancellationToken cancellationToken
        )
            => Task.FromResult<IReadOnlyCollection<MachineAlarm>>(
                _alarms.Values.Where(item => item.MachineOperationId == operationId).ToArray()
            );

        public Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
        {
            _alarms[alarm.Id] = alarm;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken)
        {
            _alarms[alarm.Id] = alarm;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryMachineRuntimeStateRepository
        : IMachineRuntimeStateRepository
    {
        private readonly Dictionary<string, MachineRuntimeState> _items = [];

        public Task<MachineRuntimeState?> GetByMachineIdAsync(
            string machineId,
            CancellationToken cancellationToken
        )
        {
            _items.TryGetValue(machineId, out MachineRuntimeState? state);
            return Task.FromResult(state);
        }

        public Task<IReadOnlyCollection<MachineRuntimeState>> GetAllAsync(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyCollection<MachineRuntimeState>>(
                _items.Values.OrderBy(item => item.MachineId).ToArray()
            );
        }

        public Task AddAsync(MachineRuntimeState state, CancellationToken cancellationToken)
        {
            _items[state.MachineId] = state;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            MachineRuntimeState state,
            int expectedVersion,
            CancellationToken cancellationToken
        )
        {
            _items[state.MachineId] = state;
            return Task.CompletedTask;
        }
    }
}
