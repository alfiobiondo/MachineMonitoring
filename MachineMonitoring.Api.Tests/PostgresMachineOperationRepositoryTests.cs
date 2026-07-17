using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresMachineOperationRepositoryTests
{
    private readonly PostgresWebApplicationFactory _factory;

    private static readonly Guid MaterialId = InMemoryProductionData.StainlessSteel304MaterialId;
    private static readonly Guid NozzleId = InMemoryProductionData.Nozzle12Id;
    private static readonly Guid DrawingFileId = InMemoryProductionData.TubeDrawingId;

    public PostgresMachineOperationRepositoryTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RestoresOperation()
    {
        // Arrange
        using IServiceScope scope = _factory.Services.CreateScope();

        IMachineOperationRepository repository =
            scope.ServiceProvider.GetRequiredService<IMachineOperationRepository>();
        IProductionLotRepository productionLotRepository =
            scope.ServiceProvider.GetRequiredService<IProductionLotRepository>();
        IWorkpieceRepository workpieceRepository =
            scope.ServiceProvider.GetRequiredService<IWorkpieceRepository>();

        ProductionLot productionLot = new(
            id: Guid.NewGuid(),
            code: "LOT-TEST-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );

        Guid workpieceId = Guid.NewGuid();
        Workpiece workpiece = new(
            id: workpieceId,
            productionLotId: productionLot.Id,
            sequenceNumber: 1,
            code: "WP-TEST-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        await productionLotRepository.AddAsync(productionLot, CancellationToken.None);
        await workpieceRepository.AddAsync(workpiece, CancellationToken.None);

        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: workpieceId,
            sequenceNumber: 1,
            machineId: "M-TEST-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        LaserCutConfiguration configuration = CreateConfiguration(operation.Id);

        // Act
        await repository.AddAsync(operation, configuration, CancellationToken.None);

        MachineOperation? restoredOperation = await repository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(restoredOperation);
        Assert.Equal(operation.Id, restoredOperation.Id);
        Assert.Equal(operation.WorkpieceId, restoredOperation.WorkpieceId);
        Assert.Equal("M-TEST-001", restoredOperation.MachineId);
        Assert.Equal(MachineOperationStatus.Queued, restoredOperation.Status);
        Assert.Equal(0, restoredOperation.ProgressPercentage);

        LaserCutConfiguration? restoredConfiguration =
            await repository.GetConfigurationByOperationIdAsync(
                operation.Id,
                CancellationToken.None
            );

        Assert.NotNull(restoredConfiguration);
        Assert.Equal(operation.Id, restoredConfiguration.OperationId);
        Assert.Equal(MaterialId, restoredConfiguration.MaterialId);
        Assert.Equal(NozzleId, restoredConfiguration.NozzleId);

        TubeGeometry restoredGeometry = Assert.IsType<TubeGeometry>(restoredConfiguration.Geometry);

        Assert.Equal(80m, restoredGeometry.OuterDiameterMillimeters);
        Assert.Equal(3m, restoredGeometry.ThicknessMillimeters);
        Assert.Equal(6000m, restoredGeometry.LengthMillimeters);
    }

    private static LaserCutConfiguration CreateConfiguration(Guid operationId)
    {
        TubeGeometry geometry = new(
            outerDiameterMillimeters: 80m,
            thicknessMillimeters: 3m,
            lengthMillimeters: 6000m
        );

        return new LaserCutConfiguration(
            id: Guid.NewGuid(),
            operationId: operationId,
            materialId: MaterialId,
            nozzleId: NozzleId,
            drawingFileId: DrawingFileId,
            geometry: geometry,
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
