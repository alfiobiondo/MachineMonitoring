using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Domain.Production;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MachineMonitoring.Api.Tests;

public sealed class OperationTransitionEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OperationTransitionEndpointTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient();

        _factory.MachineOperationRepository.Clear();
    }

    [Fact]
    public async Task PauseOperation_WhenOperationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        Guid operationId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operationId}/pause",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);

        Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);

        Assert.Equal("Resource not found", problemDetails.Title);

        Assert.Contains(operationId.ToString(), problemDetails.Detail);
    }

    [Fact]
    public async Task PauseOperation_WhenOperationIsQueued_ReturnsUnprocessableEntity()
    {
        // Arrange
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/pause",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problemDetails.Status);

        Assert.Equal("Business rule violation", problemDetails.Title);

        Assert.Contains("cannot be paused from status Queued", problemDetails.Detail);
    }

    [Fact]
    public async Task PauseOperation_WhenOperationIsRunning_ReturnsNoContent()
    {
        // Arrange
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        operation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Preparing laser");

        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/pause",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);

        Assert.Equal(MachineOperationStatus.Paused, storedOperation.Status);
    }
}
