using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Common;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class OperationListEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OperationListEndpointTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient();

        _factory.MachineOperationRepository.Clear();
        _factory.WorkpieceRepository.Clear();
        _factory.ProductionLotRepository.Clear();
    }

    [Fact]
    public async Task GetOperations_WhenRepositoryIsEmpty_ReturnsEmptyPage()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/operations");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PagedResponse<MachineOperationResponse>? result = await response.Content.ReadFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >();

        Assert.NotNull(result);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetOperations_ReturnsStoredOperations()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        MachineOperation firstOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-10)
        );

        MachineOperation secondOperation = CreateOperation(machineId: "M-002", createdAt: now);

        _factory.MachineOperationRepository.Seed(firstOperation);

        _factory.MachineOperationRepository.Seed(secondOperation);
        SeedHierarchy(firstOperation.WorkpieceId);
        SeedHierarchy(secondOperation.WorkpieceId);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/operations");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PagedResponse<MachineOperationResponse>? result = await response.Content.ReadFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >();

        Assert.NotNull(result);

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetOperations_ReturnsNewestOperationFirst()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        MachineOperation olderOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddHours(-1)
        );

        MachineOperation newerOperation = CreateOperation(machineId: "M-001", createdAt: now);

        _factory.MachineOperationRepository.Seed(olderOperation);

        _factory.MachineOperationRepository.Seed(newerOperation);
        SeedHierarchy(olderOperation.WorkpieceId);
        SeedHierarchy(newerOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse[] items = result.Items.ToArray();

        Assert.Equal(2, items.Length);

        Assert.Equal(newerOperation.Id, items[0].Id);

        Assert.Equal(olderOperation.Id, items[1].Id);
    }

    [Fact]
    public async Task GetOperations_WithMachineFilter_ReturnsMatchingOperations()
    {
        // Arrange
        MachineOperation firstMachineOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        MachineOperation secondMachineOperation = CreateOperation(
            machineId: "M-002",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.MachineOperationRepository.Seed(firstMachineOperation);

        _factory.MachineOperationRepository.Seed(secondMachineOperation);
        SeedHierarchy(firstMachineOperation.WorkpieceId);
        SeedHierarchy(secondMachineOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations?machineId=M-001");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse operation = Assert.Single(result.Items);

        Assert.Equal(firstMachineOperation.Id, operation.Id);

        Assert.Equal("M-001", operation.MachineId);

        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task GetOperations_WithStatusFilter_ReturnsMatchingOperations()
    {
        // Arrange
        MachineOperation queuedOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        MachineOperation runningOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        runningOperation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Cutting");

        MachineOperation completedOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        completedOperation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Cutting");

        completedOperation.Complete(completedAt: DateTimeOffset.UtcNow);

        _factory.MachineOperationRepository.Seed(queuedOperation);

        _factory.MachineOperationRepository.Seed(runningOperation);

        _factory.MachineOperationRepository.Seed(completedOperation);
        SeedHierarchy(queuedOperation.WorkpieceId);
        SeedHierarchy(runningOperation.WorkpieceId);
        SeedHierarchy(completedOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations?status=Completed");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse operation = Assert.Single(result.Items);

        Assert.Equal(completedOperation.Id, operation.Id);

        Assert.Equal("Completed", operation.Status);

        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task GetOperations_WithMachineAndStatusFilters_ReturnsMatchingOperations()
    {
        // Arrange
        MachineOperation matchingOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        matchingOperation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Cutting");

        MachineOperation wrongMachineOperation = CreateOperation(
            machineId: "M-002",
            createdAt: DateTimeOffset.UtcNow
        );

        wrongMachineOperation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Cutting");

        MachineOperation wrongStatusOperation = CreateOperation(
            machineId: "M-001",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.MachineOperationRepository.Seed(matchingOperation);

        _factory.MachineOperationRepository.Seed(wrongMachineOperation);

        _factory.MachineOperationRepository.Seed(wrongStatusOperation);
        SeedHierarchy(matchingOperation.WorkpieceId);
        SeedHierarchy(wrongMachineOperation.WorkpieceId);
        SeedHierarchy(wrongStatusOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations" + "?machineId=M-001" + "&status=Running");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse operation = Assert.Single(result.Items);

        Assert.Equal(matchingOperation.Id, operation.Id);
    }

    [Fact]
    public async Task GetOperations_WithPagination_ReturnsRequestedPage()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        MachineOperation oldestOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-3)
        );

        MachineOperation middleOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-2)
        );

        MachineOperation newestOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-1)
        );

        _factory.MachineOperationRepository.Seed(oldestOperation);

        _factory.MachineOperationRepository.Seed(middleOperation);

        _factory.MachineOperationRepository.Seed(newestOperation);
        SeedHierarchy(oldestOperation.WorkpieceId);
        SeedHierarchy(middleOperation.WorkpieceId);
        SeedHierarchy(newestOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations?page=1&pageSize=2");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse[] items = result.Items.ToArray();

        Assert.Equal(2, items.Length);

        Assert.Equal(newestOperation.Id, items[0].Id);

        Assert.Equal(middleOperation.Id, items[1].Id);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetOperations_SecondPage_ReturnsRemainingOperation()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        MachineOperation oldestOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-3)
        );

        MachineOperation middleOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-2)
        );

        MachineOperation newestOperation = CreateOperation(
            machineId: "M-001",
            createdAt: now.AddMinutes(-1)
        );

        _factory.MachineOperationRepository.Seed(oldestOperation);

        _factory.MachineOperationRepository.Seed(middleOperation);

        _factory.MachineOperationRepository.Seed(newestOperation);
        SeedHierarchy(oldestOperation.WorkpieceId);
        SeedHierarchy(middleOperation.WorkpieceId);
        SeedHierarchy(newestOperation.WorkpieceId);

        // Act
        PagedResponse<MachineOperationResponse>? result = await _client.GetFromJsonAsync<
            PagedResponse<MachineOperationResponse>
        >("/api/operations?page=2&pageSize=2");

        // Assert
        Assert.NotNull(result);

        MachineOperationResponse operation = Assert.Single(result.Items);

        Assert.Equal(oldestOperation.Id, operation.Id);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
    }

    [Theory]
    [InlineData("/api/operations?page=0")]
    [InlineData("/api/operations?page=-1")]
    [InlineData("/api/operations?pageSize=0")]
    [InlineData("/api/operations?pageSize=-1")]
    [InlineData("/api/operations?pageSize=101")]
    public async Task GetOperations_WithInvalidPagination_ReturnsBadRequest(string requestUri)
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(requestUri);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MachineOperation CreateOperation(string machineId, DateTimeOffset createdAt)
    {
        return new MachineOperation(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: machineId,
            type: MachineOperationType.LaserCutting,
            createdAt: createdAt
        );
    }

    private void SeedHierarchy(Guid workpieceId)
    {
        ProductionLot productionLot = new(
            id: Guid.NewGuid(),
            code: $"LOT-{workpieceId:N}",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );

        Workpiece workpiece = new(
            id: workpieceId,
            productionLotId: productionLot.Id,
            code: $"WP-{workpieceId:N}",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.ProductionLotRepository.Seed(productionLot);
        _factory.WorkpieceRepository.Seed(workpiece);
    }
}
