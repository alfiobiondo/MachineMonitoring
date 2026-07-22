using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineIncidentSimulatorTests
{
    [Fact]
    public void Options_DefaultsAreValid()
    {
        ValidateOptionsResult result = Validate(new MachineIncidentSimulatorOptions());

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, nameof(MachineIncidentSimulatorOptions.PollingIntervalSeconds))]
    [InlineData(5, -1, 0, 0, nameof(MachineIncidentSimulatorOptions.WarningProbabilityPercentage))]
    [InlineData(5, 101, 0, 0, nameof(MachineIncidentSimulatorOptions.WarningProbabilityPercentage))]
    [InlineData(5, 0, -1, 0, nameof(MachineIncidentSimulatorOptions.BlockingAlarmProbabilityPercentage))]
    [InlineData(5, 0, 101, 0, nameof(MachineIncidentSimulatorOptions.BlockingAlarmProbabilityPercentage))]
    [InlineData(5, 60, 50, 0, nameof(MachineIncidentSimulatorOptions.WarningProbabilityPercentage))]
    [InlineData(5, 0, 0, -1, nameof(MachineIncidentSimulatorOptions.MinimumSecondsBetweenIncidents))]
    public void Options_InvalidValuesAreRejected(
        int pollingIntervalSeconds,
        int warningProbabilityPercentage,
        int blockingAlarmProbabilityPercentage,
        int minimumSecondsBetweenIncidents,
        string expectedFailure
    )
    {
        ValidateOptionsResult result = Validate(
            new MachineIncidentSimulatorOptions
            {
                PollingIntervalSeconds = pollingIntervalSeconds,
                WarningProbabilityPercentage = warningProbabilityPercentage,
                BlockingAlarmProbabilityPercentage = blockingAlarmProbabilityPercentage,
                MinimumSecondsBetweenIncidents = minimumSecondsBetweenIncidents,
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(expectedFailure));
    }

    [Theory]
    [InlineData(0, MachineIncidentSimulationStatus.BlockingAlarmCreated)]
    [InlineData(19.999, MachineIncidentSimulationStatus.BlockingAlarmCreated)]
    [InlineData(20, MachineIncidentSimulationStatus.WarningCreated)]
    [InlineData(49.999, MachineIncidentSimulationStatus.WarningCreated)]
    [InlineData(50, MachineIncidentSimulationStatus.None)]
    public async Task ProcessCandidateAsync_AppliesNonOverlappingProbabilityThresholds(
        double randomValue,
        MachineIncidentSimulationStatus expectedStatus
    )
    {
        TestHarness harness = new(
            new MachineIncidentSimulatorOptions
            {
                Enabled = true,
                BlockingAlarmProbabilityPercentage = 20,
                WarningProbabilityPercentage = 30,
                MinimumSecondsBetweenIncidents = 0,
            },
            randomValue
        );
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();

        MachineIncidentSimulationResult result = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );

        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task RaiseWarningAsync_CreatesNonBlockingAlarmWithoutChangingOperationOrRuntime()
    {
        TestHarness harness = new();
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync(progress: 40);

        RaiseMachineOperationWarningResult result = await harness.WarningService.RaiseAsync(
            new RaiseMachineOperationWarningCommand(
                operation.MachineId,
                operation.Id,
                MachineIncidentSimulator.SimulatedWarningCode,
                "Temperature is rising."
            ),
            CancellationToken.None
        );

        MachineAlarm alarm = Assert.Single(
            await harness.AlarmRepository.GetByOperationIdAsync(operation.Id, CancellationToken.None)
        );
        MachineOperation storedOperation = (await harness.OperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        ))!;
        MachineRuntimeState runtime = (await harness.RuntimeRepository.GetByMachineIdAsync(
            operation.MachineId,
            CancellationToken.None
        ))!;

        Assert.Equal(RaiseMachineOperationWarningStatus.Created, result.Status);
        Assert.Equal(MachineAlarmSeverity.Warning, alarm.Severity);
        Assert.False(MachineAlarmBlockingPolicy.IsBlocking(alarm));
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal(MachineRuntimeStatus.Running, runtime.Status);
        Assert.Equal(operation.Id, runtime.CurrentOperationId);
        Assert.Equal(40, storedOperation.ProgressPercentage);
        Assert.Single(harness.NotificationPublisher.Published.OfType<MachineAlarmRaisedNotification>());
        Assert.Empty(harness.NotificationPublisher.Published.OfType<OperationStatusChangedNotification>());
        Assert.Empty(harness.NotificationPublisher.Published.OfType<MachineRuntimeStatusChangedNotification>());
    }

    [Theory]
    [InlineData(MachineAlarmStatus.Active)]
    [InlineData(MachineAlarmStatus.Acknowledged)]
    public async Task RaiseWarningAsync_WhenActiveOrAcknowledgedDuplicateExists_SkipsDuplicate(
        MachineAlarmStatus duplicateStatus
    )
    {
        TestHarness harness = new();
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();
        MachineAlarm duplicate = await harness.AddWarningAsync(operation);

        if (duplicateStatus == MachineAlarmStatus.Acknowledged)
        {
            duplicate.Acknowledge(harness.TimeProvider.GetUtcNow());
            await harness.AlarmRepository.UpdateAsync(duplicate, CancellationToken.None);
        }

        RaiseMachineOperationWarningResult result = await harness.WarningService.RaiseAsync(
            new RaiseMachineOperationWarningCommand(
                operation.MachineId,
                operation.Id,
                MachineIncidentSimulator.SimulatedWarningCode,
                "Temperature is still rising."
            ),
            CancellationToken.None
        );

        IReadOnlyCollection<MachineAlarm> alarms =
            await harness.AlarmRepository.GetByOperationIdAsync(operation.Id, CancellationToken.None);

        Assert.Equal(RaiseMachineOperationWarningStatus.SkippedDuplicate, result.Status);
        Assert.Single(alarms);
    }

    [Fact]
    public async Task RaiseWarningAsync_WhenDuplicateIsResolved_AllowsNewOccurrence()
    {
        TestHarness harness = new();
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();
        MachineAlarm duplicate = await harness.AddWarningAsync(operation);
        duplicate.Resolve(harness.TimeProvider.GetUtcNow(), "Resolved.");
        await harness.AlarmRepository.UpdateAsync(duplicate, CancellationToken.None);

        RaiseMachineOperationWarningResult result = await harness.WarningService.RaiseAsync(
            new RaiseMachineOperationWarningCommand(
                operation.MachineId,
                operation.Id,
                MachineIncidentSimulator.SimulatedWarningCode,
                "Temperature is rising again."
            ),
            CancellationToken.None
        );

        IReadOnlyCollection<MachineAlarm> alarms =
            await harness.AlarmRepository.GetByOperationIdAsync(operation.Id, CancellationToken.None);

        Assert.Equal(RaiseMachineOperationWarningStatus.Created, result.Status);
        Assert.Equal(2, alarms.Count);
    }

    [Fact]
    public async Task ProcessCandidateAsync_BlockingIncidentReusesFaultFlow()
    {
        TestHarness harness = new(
            new MachineIncidentSimulatorOptions
            {
                Enabled = true,
                BlockingAlarmProbabilityPercentage = 100,
                MinimumSecondsBetweenIncidents = 0,
            },
            0
        );
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();

        MachineIncidentSimulationResult result = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );

        MachineAlarm alarm = Assert.Single(
            await harness.AlarmRepository.GetByOperationIdAsync(operation.Id, CancellationToken.None)
        );
        MachineOperation storedOperation = (await harness.OperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        ))!;
        MachineRuntimeState runtime = (await harness.RuntimeRepository.GetByMachineIdAsync(
            operation.MachineId,
            CancellationToken.None
        ))!;

        Assert.Equal(MachineIncidentSimulationStatus.BlockingAlarmCreated, result.Status);
        Assert.Equal(MachineAlarmSeverity.Error, alarm.Severity);
        Assert.True(MachineAlarmBlockingPolicy.IsBlocking(alarm));
        Assert.Equal(MachineOperationStatus.Faulted, storedOperation.Status);
        Assert.Equal(MachineRuntimeStatus.Faulted, runtime.Status);
        Assert.Equal(operation.Id, runtime.CurrentOperationId);
        Assert.Single(harness.NotificationPublisher.Published.OfType<MachineAlarmRaisedNotification>());
        Assert.Single(harness.NotificationPublisher.Published.OfType<OperationStatusChangedNotification>());
        Assert.Single(harness.NotificationPublisher.Published.OfType<MachineRuntimeStatusChangedNotification>());
    }

    [Fact]
    public async Task ProcessCandidateAsync_UsesCooldownOnlyAfterCreatedIncident()
    {
        TestHarness harness = new(
            new MachineIncidentSimulatorOptions
            {
                Enabled = true,
                WarningProbabilityPercentage = 100,
                MinimumSecondsBetweenIncidents = 60,
            },
            0,
            0,
            0
        );
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();

        MachineIncidentSimulationResult first = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );
        MachineIncidentSimulationResult second = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );
        harness.TimeProvider.Advance(TimeSpan.FromSeconds(60));
        MachineIncidentSimulationResult third = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );

        Assert.Equal(MachineIncidentSimulationStatus.WarningCreated, first.Status);
        Assert.Equal(MachineIncidentSimulationStatus.SkippedCooldown, second.Status);
        Assert.Equal(MachineIncidentSimulationStatus.SkippedDuplicate, third.Status);
    }

    [Fact]
    public async Task ProcessCandidateAsync_DuplicateDoesNotConsumeCooldown()
    {
        TestHarness harness = new(
            new MachineIncidentSimulatorOptions
            {
                Enabled = true,
                WarningProbabilityPercentage = 100,
                MinimumSecondsBetweenIncidents = 60,
            },
            0,
            0
        );
        MachineOperation operation = await harness.CreateRunningAssignedOperationAsync();
        await harness.AddWarningAsync(operation);

        MachineIncidentSimulationResult duplicate = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );
        MachineIncidentSimulationResult stillEvaluates = await harness.Simulator.ProcessCandidateAsync(
            operation,
            CancellationToken.None
        );

        Assert.Equal(MachineIncidentSimulationStatus.SkippedDuplicate, duplicate.Status);
        Assert.Equal(MachineIncidentSimulationStatus.SkippedDuplicate, stillEvaluates.Status);
    }

    private static ValidateOptionsResult Validate(MachineIncidentSimulatorOptions options)
    {
        return new MachineIncidentSimulatorOptionsValidator().Validate(null, options);
    }

    private sealed class TestHarness
    {
        private readonly InMemoryMachineOperationEventRepository _eventRepository = new();
        private readonly InMemoryWorkpieceRepository _workpieceRepository = new();
        private readonly InMemoryProductionLotRepository _productionLotRepository = new();
        private readonly Guid _workpieceId;

        public TestHarness(
            MachineIncidentSimulatorOptions? options = null,
            params double[] randomValues
        )
        {
            Options = options ?? new MachineIncidentSimulatorOptions
            {
                Enabled = true,
                WarningProbabilityPercentage = 100,
                MinimumSecondsBetweenIncidents = 0,
            };
            RandomSource = new SequenceIncidentRandomSource(randomValues.Length == 0 ? [0] : randomValues);
            TimeProvider = new ManualTimeProvider(
                new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero)
            );

            ProductionLot lot = new(
                id: Guid.NewGuid(),
                code: "LOT-INC-001",
                plannedQuantity: 1,
                createdAt: TimeProvider.GetUtcNow()
            );
            Workpiece workpiece = new(
                id: Guid.NewGuid(),
                productionLotId: lot.Id,
                sequenceNumber: 1,
                code: "WP-INC-001",
                materialCode: "INOX-304",
                createdAt: TimeProvider.GetUtcNow()
            );
            _workpieceId = workpiece.Id;
            _productionLotRepository.AddAsync(lot, CancellationToken.None).GetAwaiter().GetResult();
            _workpieceRepository.AddAsync(workpiece, CancellationToken.None).GetAwaiter().GetResult();

            MachineOperationStartCoordinator operationStartCoordinator = new(
                OperationRepository,
                _eventRepository,
                AlarmRepository,
                RuntimeRepository,
                NotificationPublisher
            );
            ProductionSequenceService sequenceService = new(
                _productionLotRepository,
                _workpieceRepository,
                OperationRepository,
                _eventRepository,
                operationStartCoordinator,
                new NoOpProductionTransactionManager(),
                NullLogger<ProductionSequenceService>.Instance
            );
            MachineOperationApplicationService operationService = new(
                materialRepository: new InMemoryMaterialRepository(new InMemoryProductionCatalog()),
                nozzleRepository: new InMemoryNozzleRepository(new InMemoryProductionCatalog()),
                drawingFileRepository: new InMemoryDrawingFileRepository(new InMemoryProductionCatalog()),
                machineCapabilitiesRepository: new InMemoryMachineCapabilitiesRepository(new InMemoryProductionCatalog()),
                workpieceRepository: _workpieceRepository,
                machineOperationRepository: OperationRepository,
                machineOperationEventRepository: _eventRepository,
                machineAlarmRepository: AlarmRepository,
                machineRuntimeStateRepository: RuntimeRepository,
                transactionManager: new NoOpProductionTransactionManager(),
                productionSequenceService: sequenceService,
                operationStartCoordinator: operationStartCoordinator,
                configurationValidator: new LaserCutConfigurationValidator(),
                notificationPublisher: NotificationPublisher,
                logger: NullLogger<MachineOperationApplicationService>.Instance
            );

            WarningService = new MachineOperationWarningApplicationService(
                OperationRepository,
                RuntimeRepository,
                AlarmRepository,
                new NoOpProductionTransactionManager(),
                NotificationPublisher,
                TimeProvider
            );
            Simulator = new MachineIncidentSimulator(
                Microsoft.Extensions.Options.Options.Create(Options),
                RandomSource,
                new MachineIncidentCooldownTracker(
                    Microsoft.Extensions.Options.Options.Create(Options),
                    TimeProvider
                ),
                WarningService,
                operationService,
                AlarmRepository,
                NullLogger<MachineIncidentSimulator>.Instance
            );
        }

        public MachineIncidentSimulatorOptions Options { get; }

        public SequenceIncidentRandomSource RandomSource { get; }

        public ManualTimeProvider TimeProvider { get; }

        public InMemoryMachineOperationRepository OperationRepository { get; } = new();

        public InMemoryMachineAlarmRepository AlarmRepository { get; } = new();

        public InMemoryMachineRuntimeStateRepository RuntimeRepository { get; } = new();

        public RecordingNotificationPublisher NotificationPublisher { get; } = new();

        public MachineOperationWarningApplicationService WarningService { get; }

        public MachineIncidentSimulator Simulator { get; }

        public async Task<MachineOperation> CreateRunningAssignedOperationAsync(int progress = 0)
        {
            MachineOperation operation = new(
                id: Guid.NewGuid(),
                workpieceId: _workpieceId,
                sequenceNumber: 1,
                machineId: "M-001",
                type: MachineOperationType.LaserCutting,
                createdAt: TimeProvider.GetUtcNow()
            );
            operation.Start(TimeProvider.GetUtcNow(), "Preparing laser");
            if (progress > 0)
            {
                operation.UpdateProgress(progress, "Laser cutting");
            }

            await OperationRepository.AddAsync(
                operation,
                CreateConfiguration(operation.Id),
                CancellationToken.None
            );

            MachineRuntimeState runtimeState = MachineRuntimeState.CreateAvailable(
                operation.MachineId,
                TimeProvider.GetUtcNow()
            );
            runtimeState.StartOperation(operation.Id, TimeProvider.GetUtcNow());
            await RuntimeRepository.AddAsync(runtimeState, CancellationToken.None);

            return operation;
        }

        public async Task<MachineAlarm> AddWarningAsync(MachineOperation operation)
        {
            MachineAlarm alarm = new(
                id: Guid.NewGuid(),
                machineId: operation.MachineId,
                machineOperationId: operation.Id,
                code: MachineIncidentSimulator.SimulatedWarningCode,
                severity: MachineAlarmSeverity.Warning,
                message: "Existing warning.",
                raisedAt: TimeProvider.GetUtcNow()
            );

            await AlarmRepository.AddAsync(alarm, CancellationToken.None);
            return alarm;
        }

        private static LaserCutConfiguration CreateConfiguration(Guid operationId)
        {
            return new LaserCutConfiguration(
                id: Guid.NewGuid(),
                operationId: operationId,
                materialId: InMemoryProductionData.StainlessSteel304MaterialId,
                nozzleId: InMemoryProductionData.Nozzle12Id,
                drawingFileId: InMemoryProductionData.TubeDrawingId,
                geometry: new TubeGeometry(80m, 3m, 6000m),
                laserPowerWatts: 2500m,
                cuttingSpeedMillimetersPerMinute: 1200m,
                assistGas: AssistGasType.Nitrogen,
                gasPressureBar: 15m,
                focalOffsetMillimeters: -0.5m,
                numberOfPasses: 1,
                createdAt: DateTimeOffset.UtcNow
            );
        }
    }

    private sealed class SequenceIncidentRandomSource : IIncidentRandomSource
    {
        private readonly Queue<double> _values;

        public SequenceIncidentRandomSource(IEnumerable<double> values)
        {
            _values = new Queue<double>(values);
        }

        public double NextPercentage()
        {
            return _values.Count == 0 ? 99 : _values.Dequeue();
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan timeSpan)
        {
            _utcNow = _utcNow.Add(timeSpan);
        }
    }

    private sealed class RecordingNotificationPublisher : IProductionNotificationPublisher
    {
        public List<ProductionNotification> Published { get; } = [];

        public Task PublishAsync(
            ProductionNotification notification,
            CancellationToken cancellationToken
        )
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
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

    public sealed class InMemoryMachineAlarmRepository : IMachineAlarmRepository
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
                .Where(item => string.Equals(item.MachineId, machineId, StringComparison.Ordinal))
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

    public sealed class InMemoryMachineRuntimeStateRepository : IMachineRuntimeStateRepository
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

    private sealed class InMemoryMachineOperationEventRepository
        : IMachineOperationEventRepository
    {
        public Task AddAsync(
            MachineOperationEvent machineOperationEvent,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

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
