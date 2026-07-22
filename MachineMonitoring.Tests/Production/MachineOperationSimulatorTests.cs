using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineOperationSimulatorTests
{
    private readonly InMemoryMachineOperationRepository _operationRepository = new();
    private readonly InMemoryMachineAlarmRepository _alarmRepository;
    private readonly InMemoryMachineOperationEventRepository _eventRepository;
    private readonly Guid _workpieceId;
    private readonly MachineOperationApplicationService _operationService;
    private readonly InMemoryMachineRuntimeStateRepository _runtimeStateRepository = new();

    public MachineOperationSimulatorTests()
    {
        InMemoryProductionCatalog catalog = new();
        InMemoryWorkpieceRepository workpieceRepository = new();
        InMemoryProductionLotRepository productionLotRepository = new();
        _eventRepository = new InMemoryMachineOperationEventRepository();
        _alarmRepository = new InMemoryMachineAlarmRepository();
        NoOpProductionNotificationPublisher notificationPublisher = new();
        MachineOperationStartCoordinator operationStartCoordinator = new(
            machineOperationRepository: _operationRepository,
            machineOperationEventRepository: _eventRepository,
            machineAlarmRepository: _alarmRepository,
            machineRuntimeStateRepository: _runtimeStateRepository,
            notificationPublisher: notificationPublisher
        );

        ProductionSequenceService sequenceService = new(
            productionLotRepository,
            workpieceRepository,
            _operationRepository,
            _eventRepository,
            operationStartCoordinator,
            new NoOpProductionTransactionManager(),
            NullLogger<ProductionSequenceService>.Instance
        );

        _operationService = new MachineOperationApplicationService(
            materialRepository: new InMemoryMaterialRepository(catalog),
            nozzleRepository: new InMemoryNozzleRepository(catalog),
            drawingFileRepository: new InMemoryDrawingFileRepository(catalog),
            machineCapabilitiesRepository: new InMemoryMachineCapabilitiesRepository(catalog),
            workpieceRepository: workpieceRepository,
            machineOperationRepository: _operationRepository,
            machineOperationEventRepository: _eventRepository,
            machineAlarmRepository: _alarmRepository,
            machineRuntimeStateRepository: _runtimeStateRepository,
            transactionManager: new NoOpProductionTransactionManager(),
            productionSequenceService: sequenceService,
            operationStartCoordinator: operationStartCoordinator,
            configurationValidator: new Domain.Technology.LaserCutConfigurationValidator(),
            notificationPublisher: notificationPublisher,
            logger: NullLogger<MachineOperationApplicationService>.Instance
        );

        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-SIM-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece workpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: "WP-SIM-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        _workpieceId = workpiece.Id;
        productionLotRepository.AddAsync(lot, CancellationToken.None).GetAwaiter().GetResult();
        workpieceRepository.AddAsync(workpiece, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ProcessRunningOperationAsync_AdvancesOnlyOneStep()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: _workpieceId,
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        await _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        );

        MachineOperationSimulator simulator = CreateSimulator(
            progressStrategy: new FixedOperationProgressStrategy(20)
        );

        await simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal(20, storedOperation.ProgressPercentage);
    }

    [Fact]
    public async Task ProcessRunningOperationAsync_DoesNotCreateWarningsOrFaults()
    {
        MachineOperation operation = await CreatePersistedRunningOperationAsync();
        MachineOperationSimulator simulator = CreateSimulator(
            progressStrategy: new FixedOperationProgressStrategy(20)
        );

        await simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );
        IReadOnlyCollection<MachineAlarm> alarms = await _alarmRepository.GetByOperationIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal(20, storedOperation.ProgressPercentage);
        Assert.Empty(alarms);
        Assert.DoesNotContain(
            _eventRepository.Items,
            item => item.EventType == MachineOperationEventType.Faulted
        );
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenRuntimeRunningReferencesRunningOperation_ReturnsOperation()
    {
        MachineOperation operation = await CreatePersistedRunningOperationAsync();
        await AddRunningRuntimeAssignmentAsync(operation.MachineId, operation.Id);

        RecordingLogger<MachineRuntimeAssignedOperationQuery> logger = new();
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery(logger);

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        MachineOperation assignedOperation = Assert.Single(operations);
        Assert.Equal(operation.Id, assignedOperation.Id);
        Assert.Empty(logger.WarningMessages);
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenOperationRunningButNotAssignedByRuntime_IgnoresOperation()
    {
        MachineOperation operation = await CreatePersistedRunningOperationAsync();
        RecordingLogger<MachineRuntimeAssignedOperationQuery> logger = new();
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery(logger);

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.Empty(operations);
        Assert.Empty(logger.WarningMessages);
        Assert.NotNull(storedOperation);
        Assert.Equal(0, storedOperation.ProgressPercentage);
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenRuntimeRunningWithoutCurrentOperation_WarnsAndReturnsNone()
    {
        MachineRuntimeState runtimeState = MachineRuntimeState.Restore(
            machineId: "M-001",
            status: MachineRuntimeStatus.Running,
            currentOperationId: null,
            lastChangedAt: DateTimeOffset.UtcNow,
            failureReason: null,
            activeAlarmId: null,
            version: 1
        );
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);

        RecordingLogger<MachineRuntimeAssignedOperationQuery> logger = new();
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery(logger);

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        Assert.Empty(operations);
        Assert.Contains(
            logger.WarningMessages,
            message => message.Contains("Running but has no current operation assignment")
        );
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenRuntimeReferencesOperationThatIsNotRunning_WarnsAndReturnsNone()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: _workpieceId,
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
        await _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        );
        await AddRunningRuntimeAssignmentAsync(operation.MachineId, operation.Id);

        RecordingLogger<MachineRuntimeAssignedOperationQuery> logger = new();
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery(logger);

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        Assert.Empty(operations);
        Assert.Contains(
            logger.WarningMessages,
            message => message.Contains("with status Queued")
        );
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenRuntimeAndOperationMachineMismatch_WarnsAndReturnsNone()
    {
        MachineOperation operation = await CreatePersistedRunningOperationAsync(machineId: "M-002");
        await AddRunningRuntimeAssignmentAsync("M-001", operation.Id);

        RecordingLogger<MachineRuntimeAssignedOperationQuery> logger = new();
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery(logger);

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        Assert.Empty(operations);
        Assert.Contains(
            logger.WarningMessages,
            message => message.Contains("assigned to machine M-002")
        );
    }

    [Fact]
    public async Task AssignedOperationQuery_WhenTwoOperationsRunOnSameMachine_ProcessesOnlyRuntimeAssignedOperation()
    {
        MachineOperation assigned = await CreatePersistedRunningOperationAsync(sequenceNumber: 1);
        MachineOperation unassigned = await CreatePersistedRunningOperationAsync(sequenceNumber: 2);
        await AddRunningRuntimeAssignmentAsync(assigned.MachineId, assigned.Id);

        MachineOperationSimulator simulator = CreateSimulator(
            progressStrategy: new FixedOperationProgressStrategy(20)
        );
        MachineRuntimeAssignedOperationQuery query = CreateAssignedOperationQuery();

        IReadOnlyCollection<MachineOperation> operations =
            await query.GetAssignedRunningOperationsAsync(CancellationToken.None);

        foreach (MachineOperation operation in operations)
        {
            await simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);
        }

        MachineOperation? storedAssigned = await _operationRepository.GetByIdAsync(
            assigned.Id,
            CancellationToken.None
        );
        MachineOperation? storedUnassigned = await _operationRepository.GetByIdAsync(
            unassigned.Id,
            CancellationToken.None
        );

        Assert.Single(operations);
        Assert.NotNull(storedAssigned);
        Assert.NotNull(storedUnassigned);
        Assert.Equal(20, storedAssigned.ProgressPercentage);
        Assert.Equal(0, storedUnassigned.ProgressPercentage);
    }

    [Fact]
    public async Task ProcessRunningOperationAsync_IgnoresQueuedOperation()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: _workpieceId,
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
        await _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        );

        MachineOperationSimulator simulator = CreateSimulator();

        await simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Queued, storedOperation.Status);
        Assert.Equal(0, storedOperation.ProgressPercentage);
    }

    [Fact]
    public async Task ProcessRunningOperationAsync_IgnoresFaultedOperation()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: _workpieceId,
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        operation.UpdateProgress(40, "Laser cutting");
        operation.Fault("Gas pressure drop");

        await _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        );

        MachineOperationSimulator simulator = CreateSimulator();

        await simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Faulted, storedOperation.Status);
        Assert.Equal(40, storedOperation.ProgressPercentage);
    }

    private MachineOperationSimulator CreateSimulator(
        IOperationProgressStrategy? progressStrategy = null
    )
    {
        return new MachineOperationSimulator(
            _operationService,
            progressStrategy ?? new FixedOperationProgressStrategy(20),
            NullLogger<MachineOperationSimulator>.Instance
        );
    }

    private MachineRuntimeAssignedOperationQuery CreateAssignedOperationQuery(
        ILogger<MachineRuntimeAssignedOperationQuery>? logger = null
    )
    {
        return new MachineRuntimeAssignedOperationQuery(
            _runtimeStateRepository,
            _operationRepository,
            logger ?? NullLogger<MachineRuntimeAssignedOperationQuery>.Instance
        );
    }

    private async Task AddRunningRuntimeAssignmentAsync(string machineId, Guid operationId)
    {
        MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
            machineId,
            DateTimeOffset.UtcNow
        );
        runtimeState.StartOperation(operationId, DateTimeOffset.UtcNow);
        await _runtimeStateRepository.AddAsync(runtimeState, CancellationToken.None);
    }

    private async Task<MachineOperation> CreatePersistedRunningOperationAsync(
        int sequenceNumber = 1,
        string machineId = "M-001"
    )
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: _workpieceId,
            sequenceNumber: sequenceNumber,
            machineId: machineId,
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");

        await _operationRepository.AddAsync(
            operation,
            CreateConfiguration(operation.Id),
            CancellationToken.None
        );

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

    private sealed class NoOpProductionTransactionManager : IProductionTransactionManager
    {
        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken
        )
        {
            return operation(cancellationToken);
        }
    }

    private sealed class FixedOperationProgressStrategy : IOperationProgressStrategy
    {
        private readonly int _increment;

        public FixedOperationProgressStrategy(int increment)
        {
            _increment = increment;
        }

        public int GetNextIncrement()
        {
            return _increment;
        }
    }

    private sealed class InMemoryMachineOperationEventRepository
        : IMachineOperationEventRepository
    {
        public List<MachineOperationEvent> Items { get; } = [];

        public Task AddAsync(
            MachineOperationEvent machineOperationEvent,
            CancellationToken cancellationToken
        )
        {
            Items.Add(machineOperationEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<MachineOperationEvent>> GetByOperationIdAsync(
            Guid operationId,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>([]);

        public Task<IReadOnlyCollection<MachineOperationEvent>> GetByWorkpieceIdAsync(
            Guid workpieceId,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>([]);

        public Task<IReadOnlyCollection<MachineOperationEvent>> GetByProductionLotIdAsync(
            Guid productionLotId,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyCollection<MachineOperationEvent>>([]);
    }

    private sealed class InMemoryMachineAlarmRepository : IMachineAlarmRepository
    {
        private readonly List<MachineAlarm> _items = [];

        public Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken)
            => Task.FromResult(_items.SingleOrDefault(item => item.Id == alarmId));

        public Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
            string machineId,
            bool activeOnly,
            CancellationToken cancellationToken
        )
        {
            IReadOnlyCollection<MachineAlarm> alarms = _items
                .Where(item =>
                    string.Equals(item.MachineId, machineId, StringComparison.OrdinalIgnoreCase)
                )
                .Where(item => !activeOnly || item.Status != MachineAlarmStatus.Resolved)
                .ToArray();

            return Task.FromResult(alarms);
        }

        public Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
            Guid operationId,
            CancellationToken cancellationToken
        )
        {
            IReadOnlyCollection<MachineAlarm> alarms = _items
                .Where(item => item.MachineOperationId == operationId)
                .ToArray();

            return Task.FromResult(alarms);
        }

        public Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
        {
            _items.Add(alarm);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken)
        {
            int index = _items.FindIndex(item => item.Id == alarm.Id);
            if (index >= 0)
            {
                _items[index] = alarm;
            }

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
                _items.Values.ToArray()
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

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> WarningMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningMessages.Add(formatter(state, exception));
            }
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
            _items[workpiece.Id] = workpiece;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Workpiece workpiece, CancellationToken cancellationToken)
        {
            _items[workpiece.Id] = workpiece;
            return Task.CompletedTask;
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
            _items[productionLot.Id] = productionLot;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductionLot productionLot, CancellationToken cancellationToken)
        {
            _items[productionLot.Id] = productionLot;
            return Task.CompletedTask;
        }
    }

}
