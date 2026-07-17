using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineOperationSimulatorTests
{
    private readonly InMemoryMachineOperationRepository _operationRepository = new();
    private readonly Guid _workpieceId;
    private readonly MachineOperationApplicationService _operationService;
    private readonly MachineOperationSimulator _simulator;

    public MachineOperationSimulatorTests()
    {
        InMemoryProductionCatalog catalog = new();
        InMemoryWorkpieceRepository workpieceRepository = new();
        InMemoryProductionLotRepository productionLotRepository = new();
        InMemoryMachineOperationEventRepository eventRepository = new();

        ProductionSequenceService sequenceService = new(
            productionLotRepository,
            workpieceRepository,
            _operationRepository,
            eventRepository,
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
            machineOperationEventRepository: eventRepository,
            machineAlarmRepository: new InMemoryMachineAlarmRepository(),
            transactionManager: new NoOpProductionTransactionManager(),
            productionSequenceService: sequenceService,
            configurationValidator: new Domain.Technology.LaserCutConfigurationValidator(),
            logger: NullLogger<MachineOperationApplicationService>.Instance
        );

        _simulator = new MachineOperationSimulator(
            _operationService,
            new FixedOperationProgressStrategy(20),
            NullLogger<MachineOperationSimulator>.Instance
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

        await _simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal(20, storedOperation.ProgressPercentage);
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

        await _simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

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

        await _simulator.ProcessRunningOperationAsync(operation, CancellationToken.None);

        MachineOperation? storedOperation = await _operationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Faulted, storedOperation.Status);
        Assert.Equal(40, storedOperation.ProgressPercentage);
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
