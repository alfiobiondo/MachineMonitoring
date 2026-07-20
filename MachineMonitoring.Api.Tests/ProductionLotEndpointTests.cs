using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Api.Production;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class ProductionLotEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductionLotEndpointTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient();

        _factory.MachineOperationRepository.Clear();
        _factory.WorkpieceRepository.Clear();
        _factory.ProductionLotRepository.Clear();
        _factory.MachineOperationEventRepository.Clear();
        _factory.MachineAlarmRepository.Clear();
        _factory.MachineRuntimeStateRepository.Clear();
    }

    [Fact]
    public async Task CreateProductionLot_WithValidRequest_ReturnsCreated()
    {
        CreateProductionLotRequest request = new("LOT-CREATE-001", 3);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/production-lots", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CreateProductionLotResponse? created =
            await response.Content.ReadFromJsonAsync<CreateProductionLotResponse>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.ProductionLotId);
        Assert.Equal(request.Code, created.Code);
        Assert.Equal(request.PlannedQuantity, created.PlannedQuantity);
        Assert.Equal("Planned", created.Status);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(
            $"/api/production-lots/{created.ProductionLotId}",
            response.Headers.Location!.ToString(),
            StringComparison.Ordinal
        );

        ProductionLot? stored = await _factory.ProductionLotRepository.GetByIdAsync(
            created.ProductionLotId,
            CancellationToken.None
        );

        Assert.NotNull(stored);
        Assert.Equal(request.Code, stored.Code);
        Assert.Equal(request.PlannedQuantity, stored.PlannedQuantity);
        Assert.Equal(ProductionLotStatus.Planned, stored.Status);
    }

    [Fact]
    public async Task StartProductionLot_ActivatesAllWorkpiecesAndStartsFirstOperationOfEach()
    {
        // Arrange
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-001",
            plannedQuantity: 2,
            createdAt: DateTimeOffset.UtcNow
        );

        Workpiece firstWorkpiece = SeedWorkpiece(lot, "WP-001");
        Workpiece secondWorkpiece = SeedWorkpiece(lot, "WP-002");

        MachineOperation firstOperation = CreateQueuedOperation(firstWorkpiece.Id, 1);
        MachineOperation secondOperation = CreateQueuedOperation(firstWorkpiece.Id, 2);
        MachineOperation thirdOperation = CreateQueuedOperation(secondWorkpiece.Id, 1);

        _factory.MachineOperationRepository.Seed(firstOperation);
        _factory.MachineOperationRepository.Seed(secondOperation);
        _factory.MachineOperationRepository.Seed(thirdOperation);

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/production-lots/{lot.Id}/start",
            new StartProductionLotRequest("Preparing laser", null)
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedFirst = await _factory.MachineOperationRepository.GetByIdAsync(
            firstOperation.Id,
            CancellationToken.None
        );
        MachineOperation? storedSecond = await _factory.MachineOperationRepository.GetByIdAsync(
            secondOperation.Id,
            CancellationToken.None
        );
        MachineOperation? storedThird = await _factory.MachineOperationRepository.GetByIdAsync(
            thirdOperation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(storedThird);

        Assert.Equal(MachineOperationStatus.Running, storedFirst.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
        Assert.Equal(MachineOperationStatus.Running, storedThird.Status);
    }

    [Fact]
    public async Task GetProductionLot_ReturnsWorkpiecesAndOperations()
    {
        // Arrange
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: "LOT-DETAIL-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );

        Workpiece workpiece = SeedWorkpiece(lot, "WP-001");
        MachineOperation operation = CreateQueuedOperation(workpiece.Id, 1);
        _factory.MachineOperationRepository.Seed(operation);

        // Act
        ProductionLotDetailsResponse? response =
            await _client.GetFromJsonAsync<ProductionLotDetailsResponse>(
                $"/api/production-lots/{lot.Id}"
            );

        // Assert
        Assert.NotNull(response);
        Assert.Equal(lot.Id, response.Id);
        Assert.Single(response.Workpieces);
        Assert.Single(response.Workpieces.First().Operations);
    }

    private Workpiece SeedWorkpiece(ProductionLot lot, string code)
    {
        _factory.ProductionLotRepository.Seed(lot);

        Workpiece workpiece = new(
            id: Guid.NewGuid(),
            productionLotId: lot.Id,
            sequenceNumber: code.EndsWith("001", StringComparison.Ordinal) ? 1 : 2,
            code: code,
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.WorkpieceRepository.Seed(workpiece);
        return workpiece;
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
}
