using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Machines;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Domain.Production;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class MachineRuntimeEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MachineRuntimeEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task GetMachineState_WhenMachineExists_ReturnsRuntimeState()
    {
        MachineRuntimeStateResponse? response = await _client.GetFromJsonAsync<MachineRuntimeStateResponse>(
            "/api/machines/M-001/state"
        );

        Assert.NotNull(response);
        Assert.Equal("M-001", response.MachineId);
        Assert.Equal("Available", response.Status);
        Assert.Null(response.CurrentOperationId);
    }

    [Fact]
    public async Task FaultMachine_WhenMachineIsIdle_CreatesMachineAlarmAndFaultedRuntimeState()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/machines/M-002/fault",
            new FaultMachineRequest(
                Code: "MACHINE-001",
                Severity: "Error",
                Message: "Cooling circuit failure",
                OperationId: null
            )
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineRuntimeStateResponse? state = await _client.GetFromJsonAsync<MachineRuntimeStateResponse>(
            "/api/machines/M-002/state"
        );
        MachineAlarmResponse[]? alarms = await _client.GetFromJsonAsync<MachineAlarmResponse[]>(
            "/api/machines/M-002/alarms?activeOnly=true"
        );

        Assert.NotNull(state);
        Assert.NotNull(alarms);
        Assert.Equal("Faulted", state.Status);
        Assert.Null(state.CurrentOperationId);
        Assert.Single(alarms);
        Assert.Equal("Active", alarms[0].Status);
        Assert.Equal("MACHINE-001", alarms[0].Code);
    }

    [Fact]
    public async Task StartOperation_WhenMachineIsMaintenance_ReturnsUnprocessableEntity()
    {
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

        HttpResponseMessage maintenanceResponse = await _client.PostAsJsonAsync(
            "/api/machines/M-001/maintenance/start",
            new MachineReasonRequest("Planned maintenance")
        );

        Assert.Equal(HttpStatusCode.NoContent, maintenanceResponse.StatusCode);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/start",
            new StartMachineOperationRequest("Preparing laser")
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.Status);
        Assert.Contains("Machine M-001 is Maintenance", problem.Detail);
    }

    [Fact]
    public async Task StartOperation_WhenMachineIsOffline_ReturnsUnprocessableEntity()
    {
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

        HttpResponseMessage offlineResponse = await _client.PostAsJsonAsync(
            "/api/machines/M-001/offline",
            new MachineReasonRequest("Network unavailable")
        );

        Assert.Equal(HttpStatusCode.NoContent, offlineResponse.StatusCode);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/start",
            new StartMachineOperationRequest("Preparing laser")
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task FaultMachine_WhenRunningOperationSpecified_FaultsOperationAndMachine()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: "M-002",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        SeedHierarchy(operation.WorkpieceId);
        _factory.MachineOperationRepository.Seed(operation);

        await _client.PostAsJsonAsync(
            $"/api/operations/{operation.Id}/start",
            new StartMachineOperationRequest("Preparing laser")
        );

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/machines/M-002/fault",
            new FaultMachineRequest(
                Code: "MACHINE-FAULT-001",
                Severity: "Error",
                Message: "Chiller failure",
                OperationId: operation.Id
            )
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        MachineOperation? storedOperation = await _factory.MachineOperationRepository.GetByIdAsync(
            operation.Id,
            CancellationToken.None
        );
        MachineRuntimeState? runtimeState = await _factory.MachineRuntimeStateRepository.GetByMachineIdAsync(
            "M-002",
            CancellationToken.None
        );
        MachineAlarm alarm = Assert.Single(
            await _factory.MachineAlarmRepository.GetByOperationIdAsync(
                operation.Id,
                CancellationToken.None
            )
        );

        Assert.NotNull(storedOperation);
        Assert.NotNull(runtimeState);
        Assert.Equal(MachineOperationStatus.Faulted, storedOperation.Status);
        Assert.Equal(MachineRuntimeStatus.Faulted, runtimeState.Status);
        Assert.Equal(operation.Id, alarm.MachineOperationId);
    }

    [Fact]
    public async Task ResolveFirstBlockingAlarm_WhenAnotherBlockingAlarmIsStillActive_KeepsMachineFaulted()
    {
        HttpResponseMessage firstFaultResponse = await _client.PostAsJsonAsync(
            "/api/machines/M-002/fault",
            new FaultMachineRequest(
                Code: "MACHINE-001",
                Severity: "Error",
                Message: "Cooling circuit failure",
                OperationId: null
            )
        );
        HttpResponseMessage secondFaultResponse = await _client.PostAsJsonAsync(
            "/api/machines/M-002/fault",
            new FaultMachineRequest(
                Code: "MACHINE-002",
                Severity: "Critical",
                Message: "Hydraulic pressure failure",
                OperationId: null
            )
        );

        Assert.Equal(HttpStatusCode.NoContent, firstFaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondFaultResponse.StatusCode);

        MachineAlarm[] alarms = (
            await _factory.MachineAlarmRepository.GetByMachineIdAsync(
                "M-002",
                activeOnly: true,
                CancellationToken.None
            )
        ).OrderBy(item => item.RaisedAt).ToArray();

        HttpResponseMessage resolveFirstResponse = await _client.PostAsJsonAsync(
            $"/api/alarms/{alarms[0].Id}/resolve",
            new ResolveMachineAlarmRequest("First alarm resolved")
        );

        Assert.Equal(HttpStatusCode.NoContent, resolveFirstResponse.StatusCode);

        MachineRuntimeState? stillFaulted = await _factory.MachineRuntimeStateRepository.GetByMachineIdAsync(
            "M-002",
            CancellationToken.None
        );
        Assert.NotNull(stillFaulted);
        Assert.Equal(MachineRuntimeStatus.Faulted, stillFaulted.Status);

        HttpResponseMessage resolveSecondResponse = await _client.PostAsJsonAsync(
            $"/api/alarms/{alarms[1].Id}/resolve",
            new ResolveMachineAlarmRequest("Second alarm resolved")
        );

        Assert.Equal(HttpStatusCode.NoContent, resolveSecondResponse.StatusCode);

        MachineRuntimeState? availableState = await _factory.MachineRuntimeStateRepository.GetByMachineIdAsync(
            "M-002",
            CancellationToken.None
        );
        Assert.NotNull(availableState);
        Assert.Equal(MachineRuntimeStatus.Available, availableState.Status);
    }

    private void SeedHierarchy(Guid workpieceId)
    {
        ProductionLot lot = new(
            id: Guid.NewGuid(),
            code: $"LOT-{Guid.NewGuid():N}",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );
        Workpiece workpiece = new(
            id: workpieceId,
            productionLotId: lot.Id,
            sequenceNumber: 1,
            code: $"WP-{Guid.NewGuid():N}",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.ProductionLotRepository.AddAsync(lot, CancellationToken.None).GetAwaiter().GetResult();
        _factory.WorkpieceRepository.AddAsync(workpiece, CancellationToken.None).GetAwaiter().GetResult();
    }
}
