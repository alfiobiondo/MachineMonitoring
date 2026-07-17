using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class OperationHistoryEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OperationHistoryEndpointTests(CustomWebApplicationFactory factory)
    {
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
    public async Task GetOperationEvents_ReturnsOrderedTimeline()
    {
        MachineOperation operation = CreateRunningOperation();
        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        await _factory.MachineOperationEventRepository.AddAsync(
            new MachineOperationEvent(
                id: Guid.NewGuid(),
                machineOperationId: operation.Id,
                eventType: MachineOperationEventType.Resumed,
                occurredAt: DateTimeOffset.UtcNow.AddMinutes(2),
                previousStatus: MachineOperationStatus.Paused,
                newStatus: MachineOperationStatus.Running,
                progressPercentage: 50,
                phase: "Cutting",
                reason: null,
                machineAlarmId: null,
                metadata: null
            ),
            CancellationToken.None
        );
        await _factory.MachineOperationEventRepository.AddAsync(
            new MachineOperationEvent(
                id: Guid.NewGuid(),
                machineOperationId: operation.Id,
                eventType: MachineOperationEventType.Started,
                occurredAt: DateTimeOffset.UtcNow.AddMinutes(1),
                previousStatus: MachineOperationStatus.Queued,
                newStatus: MachineOperationStatus.Running,
                progressPercentage: 0,
                phase: "Preparing laser",
                reason: null,
                machineAlarmId: null,
                metadata: null
            ),
            CancellationToken.None
        );

        MachineOperationEventResponse[]? response =
            await _client.GetFromJsonAsync<MachineOperationEventResponse[]>(
                $"/api/operations/{operation.Id}/events"
            );

        Assert.NotNull(response);
        Assert.Equal(2, response.Length);
        Assert.Equal("Started", response[0].EventType);
        Assert.Equal("Resumed", response[1].EventType);
        Assert.True(response[0].OccurredAt <= response[1].OccurredAt);
    }

    [Fact]
    public async Task GetMachineAlarms_WithActiveOnly_FiltersResolvedItems()
    {
        MachineAlarm activeAlarm = new(
            id: Guid.NewGuid(),
            machineId: "M-001",
            machineOperationId: null,
            code: "ALARM-ACTIVE",
            severity: MachineAlarmSeverity.Warning,
            message: "Active alarm",
            raisedAt: DateTimeOffset.UtcNow
        );
        MachineAlarm resolvedAlarm = new(
            id: Guid.NewGuid(),
            machineId: "M-001",
            machineOperationId: null,
            code: "ALARM-RESOLVED",
            severity: MachineAlarmSeverity.Information,
            message: "Resolved alarm",
            raisedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );
        resolvedAlarm.Resolve(DateTimeOffset.UtcNow, "Handled");

        await _factory.MachineAlarmRepository.AddAsync(activeAlarm, CancellationToken.None);
        await _factory.MachineAlarmRepository.AddAsync(resolvedAlarm, CancellationToken.None);

        MachineAlarmResponse[]? response = await _client.GetFromJsonAsync<MachineAlarmResponse[]>(
            "/api/machines/M-001/alarms?activeOnly=true"
        );

        Assert.NotNull(response);
        Assert.Single(response);
        Assert.Equal("ALARM-ACTIVE", response[0].Code);
    }

    [Fact]
    public async Task FaultOperation_WithInvalidSeverity_ReturnsBadRequest()
    {
        MachineOperation operation = CreateRunningOperation();
        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/fault",
            new FaultMachineOperationRequest(
                FailureReason: "Unexpected condition",
                AlarmCode: "ALARM-INVALID",
                AlarmMessage: "Invalid severity",
                Severity: "Severe"
            )
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MachineOperation CreateRunningOperation()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
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
