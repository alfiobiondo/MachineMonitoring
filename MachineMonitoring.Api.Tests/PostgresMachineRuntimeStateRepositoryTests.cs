using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresMachineRuntimeStateRepositoryTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresMachineRuntimeStateRepositoryTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task UpdateAsync_WithTwoDifferentDbContexts_StaleSecondWriteThrowsConcurrencyException()
    {
        // Arrange
        string machineId = $"M-CONC-{Guid.NewGuid():N}"[..20];
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        using IServiceScope setupScope = _factory.Services.CreateScope();
        IMachineRuntimeStateRepository setupRepository =
            setupScope.ServiceProvider.GetRequiredService<IMachineRuntimeStateRepository>();

        MachineRuntimeState createdState = MachineRuntimeState.CreateAvailable(machineId, createdAt);
        await setupRepository.AddAsync(createdState, CancellationToken.None);

        using IServiceScope firstScope = _factory.Services.CreateScope();
        using IServiceScope secondScope = _factory.Services.CreateScope();

        IMachineRuntimeStateRepository firstRepository =
            firstScope.ServiceProvider.GetRequiredService<IMachineRuntimeStateRepository>();
        IMachineRuntimeStateRepository secondRepository =
            secondScope.ServiceProvider.GetRequiredService<IMachineRuntimeStateRepository>();

        MachineRuntimeState? firstRead = await firstRepository.GetByMachineIdAsync(
            machineId,
            CancellationToken.None
        );
        MachineRuntimeState? secondRead = await secondRepository.GetByMachineIdAsync(
            machineId,
            CancellationToken.None
        );

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);

        int firstExpectedVersion = firstRead.Version;
        firstRead.SetMaintenance(DateTimeOffset.UtcNow, "Planned maintenance");
        await firstRepository.UpdateAsync(
            firstRead,
            firstExpectedVersion,
            CancellationToken.None
        );

        int secondExpectedVersion = secondRead.Version;
        secondRead.SetOffline(DateTimeOffset.UtcNow, "Stale offline transition");

        // Act
        Func<Task> act = () =>
            secondRepository.UpdateAsync(
                secondRead,
                secondExpectedVersion,
                CancellationToken.None
            );

        // Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(act);
    }
}
