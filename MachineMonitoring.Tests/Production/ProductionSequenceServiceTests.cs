using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
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
    private readonly ProductionSequenceService _sequenceService;
    private readonly MachineOperationApplicationService _operationService;

    public ProductionSequenceServiceTests()
    {
        InMemoryProductionCatalog catalog = new();
        _eventRepository = new InMemoryMachineOperationEventRepository(
            _operationRepository,
            _workpieceRepository
        );

        _sequenceService = new ProductionSequenceService(
            _productionLotRepository,
            _workpieceRepository,
            _operationRepository,
            _eventRepository,
            new NoOpProductionTransactionManager(),
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
            machineAlarmRepository: new InMemoryMachineAlarmRepository(),
            transactionManager: new NoOpProductionTransactionManager(),
            productionSequenceService: _sequenceService,
            configurationValidator: new Domain.Technology.LaserCutConfigurationValidator(),
            logger: NullLogger<MachineOperationApplicationService>.Instance
        );
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
    public async Task StartProductionLotFromWorkpieceSequence_SkipsPreviousPendingWorkpieces()
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
        MachineOperation? storedFirstOperation = await _operationRepository.GetByIdAsync(
            firstOperation.Id,
            CancellationToken.None
        );
        MachineOperation? storedThirdOperation = await _operationRepository.GetByIdAsync(
            thirdOperation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(storedThird);
        Assert.NotNull(storedFirstOperation);
        Assert.NotNull(storedThirdOperation);
        Assert.Equal(WorkpieceStatus.Skipped, storedFirst.Status);
        Assert.Equal(WorkpieceStatus.Skipped, storedSecond.Status);
        Assert.Equal(WorkpieceStatus.Running, storedThird.Status);
        Assert.Equal(MachineOperationStatus.Skipped, storedFirstOperation.Status);
        Assert.Equal(MachineOperationStatus.Running, storedThirdOperation.Status);
    }

    [Fact]
    public async Task StartProductionLot_ActivatesAllWorkpieces()
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

        SeedOperation(CreateQueuedOperation(firstWorkpiece.Id, 1));
        SeedOperation(CreateQueuedOperation(secondWorkpiece.Id, 1));

        await _sequenceService.StartProductionLotAsync(
            lot.Id,
            "Preparing laser",
            startFromWorkpieceSequenceNumber: null,
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

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.True(storedFirst.IsSequenceActive);
        Assert.True(storedSecond.IsSequenceActive);
    }

    [Fact]
    public async Task FailedOperation_BlocksSequenceOfWorkpiece()
    {
        (_, Workpiece workpiece) = await SeedHierarchyAsync();
        MachineOperation first = CreateRunningOperation(workpiece.Id, 1);
        MachineOperation second = CreateQueuedOperation(workpiece.Id, 2);
        workpiece.StartSequence(DateTimeOffset.UtcNow);
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
        MachineOperation? storedSecond = await _operationRepository.GetByIdAsync(
            second.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedWorkpiece);
        Assert.NotNull(storedSecond);
        Assert.Equal(WorkpieceStatus.Failed, storedWorkpiece.Status);
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

    private static MachineOperation CreateQueuedOperation(Guid workpieceId, int sequenceNumber)
    {
        return new MachineOperation(
            id: Guid.NewGuid(),
            workpieceId: workpieceId,
            sequenceNumber: sequenceNumber,
            machineId: "M-001",
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
        public Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken)
            => Task.FromResult<MachineAlarm?>(null);

        public Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
            string machineId,
            bool activeOnly,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyCollection<MachineAlarm>>([]);

        public Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
            Guid operationId,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyCollection<MachineAlarm>>([]);

        public Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
