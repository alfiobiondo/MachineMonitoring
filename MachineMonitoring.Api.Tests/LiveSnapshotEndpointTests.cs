using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Machines;
using MachineMonitoring.Api.Tests.Fakes;
using MachineMonitoring.Application;
using MachineMonitoring.Domain;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class LiveSnapshotEndpointTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public LiveSnapshotEndpointTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task GetLiveSnapshot_WhenMachineDoesNotExist_ReturnsNotFound()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            CreateMachineProvider("M-LIVE-ONLY")
        );
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/machines/M-LIVE-MISSING/live-snapshot"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Resource not found", problem.Title);
    }

    [Fact]
    public async Task GetLiveSnapshot_WhenRuntimeDoesNotExist_ReturnsNullProductionContextAndDoesNotCreateRuntime()
    {
        const string machineId = "M-LIVE-ENDPOINT-NO-RUNTIME";
        DateTimeOffset snapshotAt = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

        await SeedAlarmAsync(
            machineId,
            Guid.NewGuid(),
            MachineAlarmSeverity.Warning,
            MachineAlarmStatus.Active,
            snapshotAt.AddMinutes(-3)
        );

        int runtimeCountBefore = await CountRuntimeStatesAsync(machineId);

        using WebApplicationFactory<Program> factory = CreateFactory(
            CreateMachineProvider(machineId),
            new FixedTimeProvider(snapshotAt)
        );
        using HttpClient client = factory.CreateClient();

        LiveSnapshotResponse? response = await client.GetFromJsonAsync<LiveSnapshotResponse>(
            $"/api/machines/{machineId}/live-snapshot"
        );
        LiveSnapshotAlarmResponse[] alarms = response?.ActiveAlarms.ToArray() ?? [];

        int runtimeCountAfter = await CountRuntimeStatesAsync(machineId);

        Assert.NotNull(response);
        Assert.Equal(machineId, response.Machine.Id);
        Assert.Null(response.Machine.Status);
        Assert.Null(response.Machine.LastChangedAt);
        Assert.Null(response.RuntimeVersion);
        Assert.Null(response.ProductionLot);
        Assert.Null(response.CurrentWorkpiece);
        Assert.Null(response.CurrentOperation);
        Assert.Single(alarms);
        Assert.False(alarms[0].IsBlocking);
        Assert.Equal(snapshotAt, response.SnapshotAt);
        Assert.Equal(runtimeCountBefore, runtimeCountAfter);
    }

    [Fact]
    public async Task GetLiveSnapshot_WhenRuntimeExistsWithoutCurrentOperation_ReturnsRuntimeOnly()
    {
        const string machineId = "M-LIVE-ENDPOINT-RUNTIME-ONLY";
        DateTimeOffset snapshotAt = new(2026, 7, 20, 12, 15, 0, TimeSpan.Zero);

        await SeedRuntimeStateAsync(
            machineId,
            MachineRuntimeStatus.Offline,
            null,
            snapshotAt.AddMinutes(-1),
            version: 9
        );

        using WebApplicationFactory<Program> factory = CreateFactory(
            CreateMachineProvider(machineId),
            new FixedTimeProvider(snapshotAt)
        );
        using HttpClient client = factory.CreateClient();

        LiveSnapshotResponse? response = await client.GetFromJsonAsync<LiveSnapshotResponse>(
            $"/api/machines/{machineId}/live-snapshot"
        );
        LiveSnapshotAlarmResponse[] alarms = response?.ActiveAlarms.ToArray() ?? [];

        Assert.NotNull(response);
        Assert.Equal("Offline", response.Machine.Status);
        Assert.Equal(9, response.RuntimeVersion);
        Assert.Null(response.ProductionLot);
        Assert.Null(response.CurrentWorkpiece);
        Assert.Null(response.CurrentOperation);
    }

    [Fact]
    public async Task GetLiveSnapshot_WhenCurrentOperationExists_ReturnsCompleteSnapshot()
    {
        const string machineId = "M-LIVE-ENDPOINT-FULL";
        DateTimeOffset snapshotAt = new(2026, 7, 20, 12, 30, 0, TimeSpan.Zero);
        Guid lotId = Guid.NewGuid();
        Guid workpieceId = Guid.NewGuid();
        Guid secondaryWorkpieceId = Guid.NewGuid();
        Guid currentOperationId = Guid.NewGuid();

        await SeedProductionHierarchyAsync(
            machineId,
            lotId,
            workpieceId,
            secondaryWorkpieceId,
            currentOperationId
        );
        await SeedAlarmAsync(
            machineId,
            Guid.NewGuid(),
            MachineAlarmSeverity.Warning,
            MachineAlarmStatus.Acknowledged,
            snapshotAt.AddMinutes(-10)
        );
        await SeedAlarmAsync(
            machineId,
            Guid.NewGuid(),
            MachineAlarmSeverity.Critical,
            MachineAlarmStatus.Active,
            snapshotAt.AddMinutes(-2)
        );

        using WebApplicationFactory<Program> factory = CreateFactory(
            CreateMachineProvider(machineId),
            new FixedTimeProvider(snapshotAt)
        );
        using HttpClient client = factory.CreateClient();

        LiveSnapshotResponse? response = await client.GetFromJsonAsync<LiveSnapshotResponse>(
            $"/api/machines/{machineId}/live-snapshot"
        );
        LiveSnapshotAlarmResponse[] alarms = response?.ActiveAlarms.ToArray() ?? [];

        Assert.NotNull(response);
        Assert.Equal("Running", response.Machine.Status);
        Assert.Equal(11, response.RuntimeVersion);
        Assert.NotNull(response.ProductionLot);
        Assert.Equal("Running", response.ProductionLot.Status);
        Assert.Equal(70.00m, response.ProductionLot.ProgressPercentage);
        Assert.Equal(2, response.ProductionLot.CompletedOperations);
        Assert.Equal(5, response.ProductionLot.TotalOperations);

        Assert.NotNull(response.CurrentWorkpiece);
        Assert.Equal("Running", response.CurrentWorkpiece.Status);
        Assert.Equal(1, response.CurrentWorkpiece.Position);
        Assert.Equal(2, response.CurrentWorkpiece.TotalWorkpieces);
        Assert.Equal(70.00m, response.CurrentWorkpiece.ProgressPercentage);

        Assert.NotNull(response.CurrentOperation);
        Assert.Equal(currentOperationId, response.CurrentOperation.Id);
        Assert.Equal("LaserCutting", response.CurrentOperation.Type);
        Assert.Equal("Running", response.CurrentOperation.Status);
        Assert.Equal(2, response.CurrentOperation.Position);
        Assert.Equal(2, response.CurrentOperation.TotalOperations);
        Assert.Equal(40, response.CurrentOperation.ProgressPercentage);

        Assert.Equal(2, alarms.Length);
        Assert.False(alarms[0].IsBlocking);
        Assert.True(alarms[1].IsBlocking);
        Assert.Equal(snapshotAt, response.SnapshotAt);
    }

    private WebApplicationFactory<Program> CreateFactory(
        TestMachineProvider machineProvider,
        TimeProvider? timeProvider = null
    )
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMachineProvider>();
                services.AddSingleton<IMachineProvider>(machineProvider);

                if (timeProvider is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                }
            })
        );
    }

    private static TestMachineProvider CreateMachineProvider(string machineId)
    {
        TestMachineProvider provider = new();
        provider.Seed(
            new Machine(
                id: machineId,
                name: $"Machine {machineId}",
                status: MachineStatus.Running,
                location: "Live Snapshot Hall",
                serialNumber: $"SN-{machineId}"
            )
        );
        return provider;
    }

    private async Task<int> CountRuntimeStatesAsync(string machineId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        return await dbContext.MachineRuntimeStates.CountAsync(
            item => item.MachineId == machineId,
            CancellationToken.None
        );
    }

    private async Task SeedRuntimeStateAsync(
        string machineId,
        MachineRuntimeStatus status,
        Guid? currentOperationId,
        DateTimeOffset lastChangedAt,
        int version
    )
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        dbContext.MachineRuntimeStates.Add(
            new MachineRuntimeStateRecord
            {
                MachineId = machineId,
                Status = status,
                CurrentOperationId = currentOperationId,
                LastChangedAt = lastChangedAt,
                FailureReason = null,
                ActiveAlarmId = null,
                Version = version,
            }
        );

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedAlarmAsync(
        string machineId,
        Guid alarmId,
        MachineAlarmSeverity severity,
        MachineAlarmStatus status,
        DateTimeOffset raisedAt
    )
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        dbContext.MachineAlarms.Add(
            new MachineAlarmRecord
            {
                Id = alarmId,
                MachineId = machineId,
                MachineOperationId = null,
                Code = $"ALARM-{alarmId.ToString("N")[..6]}",
                Severity = severity,
                Status = status,
                Message = $"Alarm {alarmId}",
                RaisedAt = raisedAt,
            }
        );

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedProductionHierarchyAsync(
        string machineId,
        Guid productionLotId,
        Guid currentWorkpieceId,
        Guid secondaryWorkpieceId,
        Guid currentOperationId
    )
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        dbContext.ProductionLots.Add(
            new ProductionLotRecord
            {
                Id = productionLotId,
                Code = $"LOT-{productionLotId.ToString("N")[..6]}",
                PlannedQuantity = 2,
                Status = ProductionLotStatus.Running,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
            }
        );

        dbContext.Workpieces.AddRange(
            new WorkpieceRecord
            {
                Id = currentWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 5,
                Code = "WP-LIVE-1",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Running,
                IsSequenceActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-35),
            },
            new WorkpieceRecord
            {
                Id = secondaryWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 25,
                Code = "WP-LIVE-2",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Running,
                IsSequenceActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            }
        );

        dbContext.MachineOperations.AddRange(
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = currentWorkpieceId,
                SequenceNumber = 10,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Completed,
                ProgressPercentage = 100,
                CurrentPhase = "Completed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-34),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-33),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            },
            new MachineOperationRecord
            {
                Id = currentOperationId,
                WorkpieceId = currentWorkpieceId,
                SequenceNumber = 20,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Running,
                ProgressPercentage = 40,
                CurrentPhase = "Laser cutting",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-29),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-28),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 5,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Skipped,
                ProgressPercentage = 0,
                CurrentPhase = "Skipped",
                FailureReason = "Skipped by partial start.",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-19),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-18),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 15,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Failed,
                ProgressPercentage = 80,
                CurrentPhase = "Fault during cut",
                FailureReason = "Laser unstable.",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-17),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-16),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 25,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Cancelled,
                ProgressPercentage = 30,
                CurrentPhase = "Cancelled",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-14),
            }
        );

        dbContext.MachineRuntimeStates.Add(
            new MachineRuntimeStateRecord
            {
                MachineId = machineId,
                Status = MachineRuntimeStatus.Running,
                CurrentOperationId = currentOperationId,
                LastChangedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                FailureReason = null,
                ActiveAlarmId = null,
                Version = 11,
            }
        );

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
