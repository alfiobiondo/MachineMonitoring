using System.Data.Common;
using MachineMonitoring.Api.Tests.Fakes;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Models;
using MachineMonitoring.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresLiveSnapshotQueryTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresLiveSnapshotQueryTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenMachineDoesNotExist_ThrowsResourceNotFoundException()
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        TestMachineProvider machineProvider = new();
        TimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        PostgresLiveSnapshotQuery query = new(dbContext, machineProvider, timeProvider);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            query.GetByMachineIdAsync("M-LIVE-MISSING", CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenRuntimeDoesNotExist_ReturnsNullProductionContextAndDoesNotCreateRuntime()
    {
        const string machineId = "M-LIVE-NO-RUNTIME";
        DateTimeOffset snapshotAt = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);

        TestMachineProvider machineProvider = CreateMachineProvider(machineId);

        await SeedMachineAlarmAsync(
            machineId,
            alarmId: Guid.NewGuid(),
            status: MachineAlarmStatus.Active,
            severity: MachineAlarmSeverity.Warning,
            raisedAt: snapshotAt.AddMinutes(-10)
        );

        int runtimeCountBefore = await CountRuntimeStatesAsync(machineId);

        CommandCounterInterceptor interceptor = new();
        await using MachineMonitoringDbContext dbContext = CreateDbContext(interceptor);
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            machineProvider,
            new FixedTimeProvider(snapshotAt)
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotAlarmResult[] alarms = result.ActiveAlarms.ToArray();

        int runtimeCountAfter = await CountRuntimeStatesAsync(machineId);

        Assert.Equal(machineId, result.Machine.Id);
        Assert.Null(result.Machine.Status);
        Assert.Null(result.Machine.LastChangedAt);
        Assert.Null(result.RuntimeVersion);
        Assert.Null(result.ProductionLot);
        Assert.Null(result.CurrentWorkpiece);
        Assert.Null(result.CurrentOperation);
        Assert.Single(alarms);
        Assert.Empty(result.Warnings);
        Assert.False(alarms[0].IsBlocking);
        Assert.Equal(snapshotAt, result.SnapshotAt);
        Assert.Equal(runtimeCountBefore, runtimeCountAfter);
        Assert.Equal(0, interceptor.WriteCount);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenRuntimeExistsWithoutCurrentOperation_ReturnsRuntimeOnly()
    {
        const string machineId = "M-LIVE-RUNTIME-ONLY";
        DateTimeOffset changedAt = new(2026, 7, 20, 10, 30, 0, TimeSpan.Zero);

        await SeedRuntimeStateAsync(
            machineId,
            status: MachineRuntimeStatus.Paused,
            currentOperationId: null,
            lastChangedAt: changedAt,
            version: 4
        );

        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            CreateMachineProvider(machineId),
            new FixedTimeProvider(changedAt.AddMinutes(1))
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotAlarmResult[] alarms = result.ActiveAlarms.ToArray();

        Assert.Equal(MachineRuntimeStatus.Paused, result.Machine.Status);
        Assert.Equal(changedAt, result.Machine.LastChangedAt);
        Assert.Equal(4, result.RuntimeVersion);
        Assert.Null(result.ProductionLot);
        Assert.Null(result.CurrentWorkpiece);
        Assert.Null(result.CurrentOperation);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenRuntimeIsRunningWithoutCurrentOperation_ReturnsRuntimeWarning()
    {
        const string machineId = "M-LIVE-WARN-RUNTIME-ONLY";
        DateTimeOffset changedAt = new(2026, 7, 20, 10, 45, 0, TimeSpan.Zero);

        await SeedRuntimeStateAsync(
            machineId,
            status: MachineRuntimeStatus.Running,
            currentOperationId: null,
            lastChangedAt: changedAt,
            version: 5
        );

        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            CreateMachineProvider(machineId),
            new FixedTimeProvider(changedAt.AddMinutes(1))
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotWarningResult warning = Assert.Single(result.Warnings);

        Assert.Equal("RuntimeWithoutCurrentOperation", warning.Code);
        Assert.Equal(machineId, warning.MachineId);
        Assert.Equal(changedAt, warning.DetectedAt);
        Assert.True(warning.IsActive);
        Assert.Null(warning.SourceId);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenCurrentOperationExists_ReturnsAggregatedSnapshot()
    {
        const string machineId = "M-LIVE-FULL";
        DateTimeOffset snapshotAt = new(2026, 7, 20, 11, 0, 0, TimeSpan.Zero);

        Guid productionLotId = Guid.NewGuid();
        Guid currentWorkpieceId = Guid.NewGuid();
        Guid secondaryWorkpieceId = Guid.NewGuid();
        Guid currentOperationId = Guid.NewGuid();

        await SeedProductionHierarchyAsync(
            machineId,
            productionLotId,
            currentWorkpieceId,
            secondaryWorkpieceId,
            currentOperationId
        );

        await SeedMachineAlarmAsync(
            machineId,
            alarmId: Guid.NewGuid(),
            status: MachineAlarmStatus.Acknowledged,
            severity: MachineAlarmSeverity.Warning,
            raisedAt: snapshotAt.AddMinutes(-5)
        );
        await SeedMachineAlarmAsync(
            machineId,
            alarmId: Guid.NewGuid(),
            status: MachineAlarmStatus.Active,
            severity: MachineAlarmSeverity.Error,
            raisedAt: snapshotAt.AddMinutes(-2)
        );

        CommandCounterInterceptor interceptor = new();
        await using MachineMonitoringDbContext dbContext = CreateDbContext(interceptor);
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            CreateMachineProvider(machineId),
            new FixedTimeProvider(snapshotAt)
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotAlarmResult[] alarms = result.ActiveAlarms.ToArray();

        Assert.Equal(MachineRuntimeStatus.Running, result.Machine.Status);
        Assert.Equal(7, result.RuntimeVersion);

        Assert.NotNull(result.CurrentOperation);
        Assert.Equal(currentOperationId, result.CurrentOperation.Id);
        Assert.Equal(20, result.CurrentOperation.SequenceNumber);
        Assert.Equal(2, result.CurrentOperation.Position);
        Assert.Equal(2, result.CurrentOperation.TotalOperations);
        Assert.Equal(40, result.CurrentOperation.ProgressPercentage);

        Assert.NotNull(result.CurrentWorkpiece);
        Assert.Equal(currentWorkpieceId, result.CurrentWorkpiece.Id);
        Assert.Equal(1, result.CurrentWorkpiece.Position);
        Assert.Equal(2, result.CurrentWorkpiece.TotalWorkpieces);
        Assert.Equal(70.00m, result.CurrentWorkpiece.ProgressPercentage);
        Assert.Equal(1, result.CurrentWorkpiece.CompletedOperations);
        Assert.Equal(2, result.CurrentWorkpiece.TotalOperations);

        Assert.NotNull(result.ProductionLot);
        Assert.Equal(productionLotId, result.ProductionLot.Id);
        Assert.Equal(70.00m, result.ProductionLot.ProgressPercentage);
        Assert.Equal(2, result.ProductionLot.CompletedOperations);
        Assert.Equal(5, result.ProductionLot.TotalOperations);

        Assert.Equal(2, alarms.Length);
        Assert.False(alarms[0].IsBlocking);
        Assert.True(alarms[1].IsBlocking);
        Assert.Empty(result.Warnings);
        Assert.Equal(snapshotAt, result.SnapshotAt);
        Assert.Equal(4, interceptor.ReadCount);
        Assert.Equal(0, interceptor.WriteCount);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenRuntimeReferencesOperationOnAnotherMachine_ReturnsMismatchWarning()
    {
        const string machineId = "M-LIVE-WARN-MISMATCH";
        const string operationMachineId = "M-LIVE-WARN-OTHER";
        DateTimeOffset changedAt = new(2026, 7, 20, 11, 15, 0, TimeSpan.Zero);
        Guid productionLotId = Guid.NewGuid();
        Guid currentWorkpieceId = Guid.NewGuid();
        Guid secondaryWorkpieceId = Guid.NewGuid();
        Guid currentOperationId = Guid.NewGuid();

        await SeedProductionHierarchyAsync(
            operationMachineId,
            productionLotId,
            currentWorkpieceId,
            secondaryWorkpieceId,
            currentOperationId,
            addRuntimeState: false
        );
        await SeedRuntimeStateAsync(
            machineId,
            MachineRuntimeStatus.Running,
            currentOperationId,
            changedAt,
            version: 12
        );

        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            CreateMachineProvider(machineId),
            new FixedTimeProvider(changedAt.AddMinutes(1))
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotWarningResult warning = Assert.Single(result.Warnings);

        Assert.Equal("RuntimeOperationMismatch", warning.Code);
        Assert.Equal(machineId, warning.MachineId);
        Assert.Equal(currentOperationId.ToString(), warning.SourceId);
        Assert.Contains(operationMachineId, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenAnotherOperationIsRunningForMachine_ReturnsOrphanWarning()
    {
        const string machineId = "M-LIVE-WARN-ORPHAN";
        DateTimeOffset startedAt = new(2026, 7, 20, 11, 20, 0, TimeSpan.Zero);
        Guid productionLotId = Guid.NewGuid();
        Guid currentWorkpieceId = Guid.NewGuid();
        Guid secondaryWorkpieceId = Guid.NewGuid();
        Guid currentOperationId = Guid.NewGuid();
        Guid orphanOperationId = Guid.NewGuid();

        await SeedProductionHierarchyAsync(
            machineId,
            productionLotId,
            currentWorkpieceId,
            secondaryWorkpieceId,
            currentOperationId
        );
        await SeedRunningOperationAsync(
            machineId,
            secondaryWorkpieceId,
            orphanOperationId,
            sequenceNumber: 35,
            startedAt
        );

        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        PostgresLiveSnapshotQuery query = new(
            dbContext,
            CreateMachineProvider(machineId),
            new FixedTimeProvider(startedAt.AddMinutes(1))
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);
        LiveSnapshotWarningResult warning = Assert.Single(result.Warnings);

        Assert.Equal("OrphanRunningOperation", warning.Code);
        Assert.Equal(machineId, warning.MachineId);
        Assert.Equal(orphanOperationId.ToString(), warning.SourceId);
        Assert.Equal(startedAt, warning.DetectedAt);
    }

    [Fact]
    public async Task GetByMachineIdAsync_KeepsQueryCountConstantAsWorkpiecesGrow()
    {
        CommandCounterInterceptor interceptor = new();

        const string singleMachineId = "M-LIVE-COUNT-1";
        Guid singleLotId = Guid.NewGuid();
        Guid singleWorkpieceId = Guid.NewGuid();
        Guid singleCurrentOperationId = Guid.NewGuid();
        await SeedScenarioForQueryCountAsync(
            machineId: singleMachineId,
            productionLotId: singleLotId,
            currentWorkpieceId: singleWorkpieceId,
            currentOperationId: singleCurrentOperationId,
            totalWorkpieces: 1
        );

        await using (MachineMonitoringDbContext dbContext = CreateDbContext(interceptor))
        {
            PostgresLiveSnapshotQuery query = new(
                dbContext,
                CreateMachineProvider(singleMachineId),
                new FixedTimeProvider(DateTimeOffset.UtcNow)
            );

            interceptor.Reset();
            _ = await query.GetByMachineIdAsync(singleMachineId, CancellationToken.None);
            Assert.Equal(4, interceptor.ReadCount);
        }

        int singleScenarioReadCount = interceptor.ReadCount;

        const string manyMachineId = "M-LIVE-COUNT-4";
        Guid manyLotId = Guid.NewGuid();
        Guid manyWorkpieceId = Guid.NewGuid();
        Guid manyCurrentOperationId = Guid.NewGuid();
        await SeedScenarioForQueryCountAsync(
            machineId: manyMachineId,
            productionLotId: manyLotId,
            currentWorkpieceId: manyWorkpieceId,
            currentOperationId: manyCurrentOperationId,
            totalWorkpieces: 4
        );

        await using (MachineMonitoringDbContext dbContext = CreateDbContext(interceptor))
        {
            PostgresLiveSnapshotQuery query = new(
                dbContext,
                CreateMachineProvider(manyMachineId),
                new FixedTimeProvider(DateTimeOffset.UtcNow)
            );

            interceptor.Reset();
            _ = await query.GetByMachineIdAsync(manyMachineId, CancellationToken.None);
            Assert.Equal(singleScenarioReadCount, interceptor.ReadCount);
        }
    }

    [Fact]
    public async Task GetByMachineIdAsync_CalculatesLotProgressFromAllOperationsNotWorkpieceAverage()
    {
        const string machineId = "M-LIVE-WEIGHTED";
        Guid productionLotId = Guid.NewGuid();
        Guid currentWorkpieceId = Guid.NewGuid();
        Guid secondaryWorkpieceId = Guid.NewGuid();
        Guid currentOperationId = Guid.NewGuid();

        await using MachineMonitoringDbContext dbContext = CreateDbContext();

        dbContext.ProductionLots.Add(
            new ProductionLotRecord
            {
                Id = productionLotId,
                Code = "LOT-WEIGHTED",
                PlannedQuantity = 2,
                Status = ProductionLotStatus.Running,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-50),
            }
        );

        dbContext.Workpieces.AddRange(
            new WorkpieceRecord
            {
                Id = currentWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 10,
                Code = "WP-A",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Completed,
                IsSequenceActive = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-45),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-35),
            },
            new WorkpieceRecord
            {
                Id = secondaryWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 20,
                Code = "WP-B",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Running,
                IsSequenceActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            }
        );

        dbContext.MachineOperations.AddRange(
            new MachineOperationRecord
            {
                Id = currentOperationId,
                WorkpieceId = currentWorkpieceId,
                SequenceNumber = 10,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Completed,
                ProgressPercentage = 100,
                CurrentPhase = "Completed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-44),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-43),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 10,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Queued,
                ProgressPercentage = 0,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-29),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 20,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Queued,
                ProgressPercentage = 0,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-28),
            },
            new MachineOperationRecord
            {
                Id = Guid.NewGuid(),
                WorkpieceId = secondaryWorkpieceId,
                SequenceNumber = 30,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Queued,
                ProgressPercentage = 0,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-27),
            }
        );

        dbContext.MachineRuntimeStates.Add(
            new MachineRuntimeStateRecord
            {
                MachineId = machineId,
                Status = MachineRuntimeStatus.Running,
                CurrentOperationId = currentOperationId,
                LastChangedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Version = 5,
            }
        );

        await dbContext.SaveChangesAsync(CancellationToken.None);

        PostgresLiveSnapshotQuery query = new(
            CreateDbContext(),
            CreateMachineProvider(machineId),
            new FixedTimeProvider(DateTimeOffset.UtcNow)
        );

        LiveSnapshotResult result = await query.GetByMachineIdAsync(machineId, CancellationToken.None);

        Assert.NotNull(result.ProductionLot);
        Assert.Equal(4, result.ProductionLot.TotalOperations);
        Assert.Equal(1, result.ProductionLot.CompletedOperations);
        Assert.Equal(25.00m, result.ProductionLot.ProgressPercentage);
    }

    private MachineMonitoringDbContext CreateDbContext(DbCommandInterceptor? interceptor = null)
    {
        DbContextOptionsBuilder<MachineMonitoringDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(_factory.ConnectionString);

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new MachineMonitoringDbContext(optionsBuilder.Options);
    }

    private static TestMachineProvider CreateMachineProvider(string machineId)
    {
        TestMachineProvider provider = new();
        provider.Seed(
            new Machine(
                id: machineId,
                name: $"Machine {machineId}",
                status: MachineStatus.Running,
                location: "Live Test Hall",
                serialNumber: $"SN-{machineId}"
            )
        );

        return provider;
    }

    private async Task<int> CountRuntimeStatesAsync(string machineId)
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();
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
        await using MachineMonitoringDbContext dbContext = CreateDbContext();
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

    private async Task SeedMachineAlarmAsync(
        string machineId,
        Guid alarmId,
        MachineAlarmStatus status,
        MachineAlarmSeverity severity,
        DateTimeOffset raisedAt
    )
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();
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
        Guid currentOperationId,
        bool addRuntimeState = true
    )
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();

        dbContext.ProductionLots.Add(
            new ProductionLotRecord
            {
                Id = productionLotId,
                Code = $"LOT-{productionLotId.ToString("N")[..6]}",
                PlannedQuantity = 2,
                Status = ProductionLotStatus.Running,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-50),
            }
        );

        dbContext.Workpieces.AddRange(
            new WorkpieceRecord
            {
                Id = currentWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 5,
                Code = "WP-CURRENT",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Running,
                IsSequenceActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-45),
            },
            new WorkpieceRecord
            {
                Id = secondaryWorkpieceId,
                ProductionLotId = productionLotId,
                SequenceNumber = 30,
                Code = "WP-SECONDARY",
                MaterialCode = "INOX-304",
                Status = WorkpieceStatus.Running,
                IsSequenceActive = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-39),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-35),
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-34),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-33),
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-29),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-28),
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
                FailureReason = "Laser power unstable.",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-27),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-26),
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
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-25),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-24),
            }
        );

        if (addRuntimeState)
        {
            dbContext.MachineRuntimeStates.Add(
                new MachineRuntimeStateRecord
                {
                    MachineId = machineId,
                    Status = MachineRuntimeStatus.Running,
                    CurrentOperationId = currentOperationId,
                    LastChangedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
                    FailureReason = null,
                    ActiveAlarmId = null,
                    Version = 7,
                }
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedRunningOperationAsync(
        string machineId,
        Guid workpieceId,
        Guid operationId,
        int sequenceNumber,
        DateTimeOffset startedAt
    )
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();
        dbContext.MachineOperations.Add(
            new MachineOperationRecord
            {
                Id = operationId,
                WorkpieceId = workpieceId,
                SequenceNumber = sequenceNumber,
                MachineId = machineId,
                Type = MachineOperationType.LaserCutting,
                Status = MachineOperationStatus.Running,
                ProgressPercentage = 10,
                CurrentPhase = "Unexpected running operation",
                CreatedAt = startedAt.AddMinutes(-1),
                StartedAt = startedAt,
            }
        );

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedScenarioForQueryCountAsync(
        string machineId,
        Guid productionLotId,
        Guid currentWorkpieceId,
        Guid currentOperationId,
        int totalWorkpieces
    )
    {
        await using MachineMonitoringDbContext dbContext = CreateDbContext();

        dbContext.ProductionLots.Add(
            new ProductionLotRecord
            {
                Id = productionLotId,
                Code = $"LOT-{productionLotId.ToString("N")[..6]}",
                PlannedQuantity = totalWorkpieces,
                Status = ProductionLotStatus.Running,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            }
        );

        for (int index = 0; index < totalWorkpieces; index++)
        {
            Guid workpieceId = index == 0 ? currentWorkpieceId : Guid.NewGuid();
            int workpieceSequenceNumber = (index + 1) * 10;
            dbContext.Workpieces.Add(
                new WorkpieceRecord
                {
                    Id = workpieceId,
                    ProductionLotId = productionLotId,
                    SequenceNumber = workpieceSequenceNumber,
                    Code = $"WP-{machineId}-{index + 1}",
                    MaterialCode = "INOX-304",
                    Status = index == 0 ? WorkpieceStatus.Running : WorkpieceStatus.Pending,
                    IsSequenceActive = index == 0,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                    StartedAt = index == 0 ? DateTimeOffset.UtcNow.AddMinutes(-20) : null,
                }
            );

            dbContext.MachineOperations.Add(
                new MachineOperationRecord
                {
                    Id = index == 0 ? currentOperationId : Guid.NewGuid(),
                    WorkpieceId = workpieceId,
                    SequenceNumber = 10,
                    MachineId = machineId,
                    Type = MachineOperationType.LaserCutting,
                    Status = index == 0
                        ? MachineOperationStatus.Running
                        : MachineOperationStatus.Queued,
                    ProgressPercentage = index == 0 ? 45 : 0,
                    CurrentPhase = index == 0 ? "Laser cutting" : null,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-(30 - index)),
                    StartedAt = index == 0 ? DateTimeOffset.UtcNow.AddMinutes(-15) : null,
                }
            );
        }

        dbContext.MachineRuntimeStates.Add(
            new MachineRuntimeStateRecord
            {
                MachineId = machineId,
                Status = MachineRuntimeStatus.Running,
                CurrentOperationId = currentOperationId,
                LastChangedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Version = 3,
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

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public void Reset()
        {
            ReadCount = 0;
            WriteCount = 0;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
        )
        {
            Track(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            Track(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result
        )
        {
            Track(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Track(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Track(DbCommand command)
        {
            string sql = command.CommandText.TrimStart();

            if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                ReadCount++;
                return;
            }

            if (
                sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                || sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                || sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
            )
            {
                WriteCount++;
            }
        }
    }
}
