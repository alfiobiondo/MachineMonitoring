using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Api.Production;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class WorkpieceEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WorkpieceEndpointTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient();

        _factory.MachineOperationRepository.Clear();
        _factory.WorkpieceRepository.Clear();
        _factory.ProductionLotRepository.Clear();
        _factory.MachineOperationEventRepository.Clear();
        _factory.MachineAlarmRepository.Clear();
    }

    [Fact]
    public async Task StartWorkpiece_StartsOnlyFirstQueuedOperation()
    {
        // Arrange
        Workpiece workpiece = SeedWorkpieceHierarchy();

        MachineOperation firstOperation = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation secondOperation = CreateQueuedOperation(workpiece.Id, 2);

        _factory.MachineOperationRepository.Seed(firstOperation);
        _factory.MachineOperationRepository.Seed(secondOperation);

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/workpieces/{workpiece.Id}/start",
            new StartWorkpieceRequest("Preparing laser", null)
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
        Workpiece? storedWorkpiece = await _factory.WorkpieceRepository.GetByIdAsync(
            workpiece.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(storedWorkpiece);

        Assert.Equal(MachineOperationStatus.Running, storedFirst.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedSecond.Status);
        Assert.True(storedWorkpiece.IsSequenceActive);
    }

    [Fact]
    public async Task CompleteFirstOperation_WhenWorkpieceSequenceIsActive_StartsNextOperationOfSameWorkpiece()
    {
        // Arrange
        Workpiece targetWorkpiece = SeedWorkpieceHierarchy();
        Workpiece otherWorkpiece = SeedWorkpieceHierarchy();

        MachineOperation firstTarget = CreateRunningOperation(targetWorkpiece.Id, 1);
        MachineOperation secondTarget = CreateQueuedOperation(targetWorkpiece.Id, 2);
        MachineOperation otherQueued = CreateQueuedOperation(otherWorkpiece.Id, 1);

        targetWorkpiece.StartSequence(DateTimeOffset.UtcNow);
        await _factory.WorkpieceRepository.UpdateAsync(targetWorkpiece, CancellationToken.None);

        _factory.MachineOperationRepository.Seed(firstTarget);
        _factory.MachineOperationRepository.Seed(secondTarget);
        _factory.MachineOperationRepository.Seed(otherQueued);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{firstTarget.Id}/complete",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedSecond = await _factory.MachineOperationRepository.GetByIdAsync(
            secondTarget.Id,
            CancellationToken.None
        );
        MachineOperation? storedOther = await _factory.MachineOperationRepository.GetByIdAsync(
            otherQueued.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedSecond);
        Assert.NotNull(storedOther);

        Assert.Equal(MachineOperationStatus.Running, storedSecond.Status);
        Assert.Equal(MachineOperationStatus.Queued, storedOther.Status);
    }

    [Fact]
    public async Task GetWorkpiece_ReturnsOrderedOperationsAndSequenceState()
    {
        // Arrange
        Workpiece workpiece = SeedWorkpieceHierarchy();
        workpiece.StartSequence(DateTimeOffset.UtcNow);
        await _factory.WorkpieceRepository.UpdateAsync(workpiece, CancellationToken.None);

        MachineOperation firstOperation = CreateQueuedOperation(workpiece.Id, 1);
        MachineOperation secondOperation = CreateQueuedOperation(workpiece.Id, 2);

        _factory.MachineOperationRepository.Seed(secondOperation);
        _factory.MachineOperationRepository.Seed(firstOperation);

        // Act
        WorkpieceDetailsResponse? response = await _client.GetFromJsonAsync<WorkpieceDetailsResponse>(
            $"/api/workpieces/{workpiece.Id}"
        );

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSequenceActive);
        Assert.Equal(1, response.SequenceNumber);
        Assert.Equal(2, response.Operations.Count);
        Assert.Equal(1, response.Operations.First().SequenceNumber);
    }

    private Workpiece SeedWorkpieceHierarchy()
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: $"LOT-{Guid.NewGuid():N}",
            plannedQuantity: 2,
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

        _factory.ProductionLotRepository.Seed(lot);
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

    private static MachineOperation CreateRunningOperation(Guid workpieceId, int sequenceNumber)
    {
        MachineOperation operation = CreateQueuedOperation(workpieceId, sequenceNumber);
        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        operation.UpdateProgress(50, "Laser cutting");
        return operation;
    }
}
