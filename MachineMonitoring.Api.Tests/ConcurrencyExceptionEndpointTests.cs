using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Machines;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class ConcurrencyExceptionEndpointTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public ConcurrencyExceptionEndpointTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task StartMaintenance_WhenRuntimeStateWasConcurrentlyModified_ReturnsConflictProblemDetails()
    {
        const string machineId = "M-001";

        await ResetMachineRuntimeStateAsync(machineId);

        using WebApplicationFactory<Program> conflictFactory = _factory.WithWebHostBuilder(
            builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IMachineRuntimeStateRepository>();
                    services.AddScoped<
                        IMachineRuntimeStateRepository,
                        ConcurrencyConflictMachineRuntimeStateRepository
                    >();
                })
        );

        using HttpClient client = conflictFactory.CreateClient();

        MachineRuntimeStateResponse? initialState =
            await client.GetFromJsonAsync<MachineRuntimeStateResponse>(
                $"/api/machines/{machineId}/state"
            );

        Assert.NotNull(initialState);

        try
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"/api/machines/{machineId}/maintenance/start",
                new MachineReasonRequest("Planned maintenance")
            );

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType
            );

            ProblemDetails? problemDetails =
                await response.Content.ReadFromJsonAsync<ProblemDetails>();

            Assert.NotNull(problemDetails);
            Assert.Equal(StatusCodes.Status409Conflict, problemDetails.Status);
            Assert.Equal("Concurrency conflict", problemDetails.Title);
            Assert.Equal(
                "The resource was modified by another request. Reload it and retry.",
                problemDetails.Detail
            );
            Assert.DoesNotContain(
                nameof(DbUpdateConcurrencyException),
                problemDetails.Detail,
                StringComparison.Ordinal
            );
            Assert.DoesNotContain(
                "expected to affect",
                problemDetails.Detail,
                StringComparison.OrdinalIgnoreCase
            );
        }
        finally
        {
            await ResetMachineRuntimeStateAsync(machineId);
        }
    }

    private async Task ResetMachineRuntimeStateAsync(string machineId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        Infrastructure.Persistence.Models.MachineRuntimeStateRecord? record =
            await dbContext.MachineRuntimeStates.SingleOrDefaultAsync(
                item => item.MachineId == machineId,
                CancellationToken.None
            );

        if (record is null)
        {
            return;
        }

        record.Status = MachineRuntimeStatus.Available;
        record.CurrentOperationId = null;
        record.FailureReason = null;
        record.ActiveAlarmId = null;
        record.LastChangedAt = DateTimeOffset.UtcNow;
        record.Version += 1;

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class ConcurrencyConflictMachineRuntimeStateRepository
        : IMachineRuntimeStateRepository
    {
        private readonly DbContextOptions<MachineMonitoringDbContext> _options;
        private readonly PostgresMachineRuntimeStateRepository _innerRepository;

        public ConcurrencyConflictMachineRuntimeStateRepository(
            MachineMonitoringDbContext dbContext,
            DbContextOptions<MachineMonitoringDbContext> options
        )
        {
            _options = options;
            _innerRepository = new PostgresMachineRuntimeStateRepository(dbContext);
        }

        public Task<MachineRuntimeState?> GetByMachineIdAsync(
            string machineId,
            CancellationToken cancellationToken
        ) => _innerRepository.GetByMachineIdAsync(machineId, cancellationToken);

        public Task<IReadOnlyCollection<MachineRuntimeState>> GetAllAsync(
            CancellationToken cancellationToken
        ) => _innerRepository.GetAllAsync(cancellationToken);

        public Task AddAsync(MachineRuntimeState state, CancellationToken cancellationToken) =>
            _innerRepository.AddAsync(state, cancellationToken);

        public async Task UpdateAsync(
            MachineRuntimeState state,
            int expectedVersion,
            CancellationToken cancellationToken
        )
        {
            if (state.MachineId == "M-001")
            {
                await ForceConcurrentUpdateAsync(state.MachineId, cancellationToken);
            }

            await _innerRepository.UpdateAsync(state, expectedVersion, cancellationToken);
        }

        private async Task ForceConcurrentUpdateAsync(
            string machineId,
            CancellationToken cancellationToken
        )
        {
            await using MachineMonitoringDbContext concurrentDbContext = new(_options);

            Infrastructure.Persistence.Models.MachineRuntimeStateRecord record =
                await concurrentDbContext.MachineRuntimeStates.SingleAsync(
                    item => item.MachineId == machineId,
                    cancellationToken
                );

            record.Status = MachineRuntimeStatus.Offline;
            record.CurrentOperationId = null;
            record.FailureReason = "Concurrent update from integration test.";
            record.ActiveAlarmId = null;
            record.LastChangedAt = DateTimeOffset.UtcNow;
            record.Version += 1;

            await concurrentDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
