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
    private readonly ProductionSequenceService _sequenceService;
    private readonly MachineOperationApplicationService _operationService;

    public ProductionSequenceServiceTests()
    {
        InMemoryProductionCatalog catalog = new();

        _sequenceService = new ProductionSequenceService(
            _productionLotRepository,
            _workpieceRepository,
            _operationRepository,
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
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece secondWorkpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
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
}
