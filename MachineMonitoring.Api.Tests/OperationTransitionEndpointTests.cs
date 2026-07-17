using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Domain.Production;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class OperationTransitionEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OperationTransitionEndpointTests(CustomWebApplicationFactory factory)
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
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        SeedHierarchy(operation.WorkpieceId);
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
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
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

    [Fact]
    public async Task StartOperation_WhenOperationIsQueued_ReturnsNoContent()
    {
        // Arrange
        MachineOperation operation = CreateQueuedOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        StartMachineOperationRequest request = new(InitialPhase: "Preparing laser");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/start",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal("Preparing laser", storedOperation.CurrentPhase);
        Assert.NotNull(storedOperation.StartedAt);

        Workpiece? workpiece = await _factory.WorkpieceRepository.GetByIdAsync(
            operation.WorkpieceId,
            CancellationToken.None
        );

        Assert.NotNull(workpiece);
        Assert.False(workpiece.IsSequenceActive);
    }

    [Fact]
    public async Task StartOperation_WhenPreviousOperationIsNotCompleted_ReturnsUnprocessableEntity()
    {
        // Arrange
        Guid workpieceId = Guid.NewGuid();
        SeedHierarchy(workpieceId);

        MachineOperation firstOperation = new(
            id: Guid.NewGuid(),
            workpieceId: workpieceId,
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        MachineOperation secondOperation = new(
            id: Guid.NewGuid(),
            workpieceId: workpieceId,
            sequenceNumber: 2,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(1)
        );

        _factory.MachineOperationRepository.Seed(firstOperation);
        _factory.MachineOperationRepository.Seed(secondOperation);

        StartMachineOperationRequest request = new(InitialPhase: "Preparing laser");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{secondOperation.Id}/start",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Business rule violation", problemDetails.Title);
        Assert.Contains("previous operation", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartOperation_WhenOperationIsRunning_ReturnsUnprocessableEntity()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        StartMachineOperationRequest request = new(InitialPhase: "Restarting");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/start",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Business rule violation", problemDetails.Title);
        Assert.Contains("cannot be started from status Running", problemDetails.Detail);
    }

    [Fact]
    public async Task UpdateProgress_WhenOperationIsRunning_UpdatesOperation()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        UpdateMachineOperationProgressRequest request = new(
            ProgressPercentage: 35,
            CurrentPhase: "Laser cutting"
        );

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/progress",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
        Assert.Equal(35, storedOperation.ProgressPercentage);
        Assert.Equal("Laser cutting", storedOperation.CurrentPhase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task UpdateProgress_WithInvalidPercentage_ReturnsBadRequest(int progressPercentage)
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        _factory.MachineOperationRepository.Seed(operation);

        UpdateMachineOperationProgressRequest request = new(
            ProgressPercentage: progressPercentage,
            CurrentPhase: "Laser cutting"
        );

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/progress",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Invalid request", problemDetails.Title);
    }

    [Fact]
    public async Task CompleteOperation_WhenOperationIsRunning_CompletesOperation()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        operation.UpdateProgress(progressPercentage: 75, currentPhase: "Finishing cut");

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/complete",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Completed, storedOperation.Status);
        Assert.Equal(100, storedOperation.ProgressPercentage);
        Assert.Equal("Completed", storedOperation.CurrentPhase);
        Assert.NotNull(storedOperation.CompletedAt);
    }

    [Fact]
    public async Task FailOperation_WhenOperationIsRunning_StoresFailureReason()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        FailMachineOperationRequest request = new(FailureReason: "Laser source unavailable");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/fail",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Failed, storedOperation.Status);
        Assert.Equal("Laser source unavailable", storedOperation.FailureReason);
    }

    [Fact]
    public async Task CancelOperation_WhenOperationIsQueued_CancelsOperation()
    {
        // Arrange
        MachineOperation operation = CreateQueuedOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/cancel",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Cancelled, storedOperation.Status);
    }

    [Fact]
    public async Task CancelOperation_WhenOperationIsCompleted_ReturnsUnprocessableEntity()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        operation.Complete(DateTimeOffset.UtcNow);

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/cancel",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Business rule violation", problemDetails.Title);
        Assert.Contains("cannot be cancelled from status Completed", problemDetails.Detail);
    }

    [Fact]
    public async Task ResumeOperation_WhenOperationIsPaused_ReturnsNoContent()
    {
        // Arrange
        MachineOperation operation = CreateRunningOperation();

        operation.Pause();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/operations/{operation.Id}/resume",
            content: null
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Running, storedOperation.Status);
    }

    [Fact]
    public async Task FaultOperation_WhenOperationIsRunning_SetsFaultedAndCreatesActiveAlarm()
    {
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        FaultMachineOperationRequest request = new(
            FailureReason: "Assist gas pressure drop",
            AlarmCode: "ALARM-001",
            AlarmMessage: "Gas pressure is below threshold.",
            Severity: "Warning"
        );

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/fault",
            request
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );
        MachineAlarm[] alarms = (
            await _factory.MachineAlarmRepository.GetByOperationIdAsync(
                operation.Id,
                CancellationToken.None
            )
        ).ToArray();
        MachineOperationEvent[] events = (
            await _factory.MachineOperationEventRepository.GetByOperationIdAsync(
                operation.Id,
                CancellationToken.None
            )
        ).ToArray();

        Assert.NotNull(storedOperation);
        Assert.Equal(MachineOperationStatus.Faulted, storedOperation.Status);
        Assert.Equal("Assist gas pressure drop", storedOperation.FailureReason);
        Assert.Single(alarms);
        Assert.Equal(MachineAlarmStatus.Active, alarms[0].Status);
        Assert.Contains(events, item => item.EventType == MachineOperationEventType.Faulted);
    }

    [Fact]
    public async Task ResolveAlarm_WhenOperationIsFaulted_MovesOperationToPaused()
    {
        MachineOperation operation = CreateRunningOperation();

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/fault",
            new FaultMachineOperationRequest(
                FailureReason: "Assist gas pressure drop",
                AlarmCode: "ALARM-001",
                AlarmMessage: "Gas pressure is below threshold.",
                Severity: "Warning"
            )
        );

        MachineAlarm alarm = (
            await _factory.MachineAlarmRepository.GetByOperationIdAsync(
                operation.Id,
                CancellationToken.None
            )
        ).Single();

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/alarms/{alarm.Id}/resolve",
            new ResolveMachineAlarmRequest("Pressure stabilized")
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );
        MachineAlarm? storedAlarm = await _factory.MachineAlarmRepository.GetByIdAsync(
            alarm.Id,
            CancellationToken.None
        );

        Assert.NotNull(storedOperation);
        Assert.NotNull(storedAlarm);
        Assert.Equal(MachineOperationStatus.Paused, storedOperation.Status);
        Assert.Equal("Assist gas pressure drop", storedOperation.FailureReason);
        Assert.Equal(MachineAlarmStatus.Resolved, storedAlarm.Status);
        Assert.Equal("Pressure stabilized", storedAlarm.ResolutionNotes);
    }

    private static MachineOperation CreateQueuedOperation()
    {
        return new MachineOperation(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );
    }

    private static MachineOperation CreateRunningOperation()
    {
        MachineOperation operation = CreateQueuedOperation();

        operation.Start(startedAt: DateTimeOffset.UtcNow, initialPhase: "Preparing laser");

        return operation;
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
            sequenceNumber: 1,
            code: $"WP-{workpieceId:N}",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.ProductionLotRepository.Seed(productionLot);
        _factory.WorkpieceRepository.Seed(workpiece);
    }
}
