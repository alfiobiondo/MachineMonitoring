using MachineMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresMigrationTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresMigrationTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task Database_HasNoPendingMigrations()
    {
        // Arrange
        using IServiceScope scope = _factory.Services.CreateScope();

        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        // Act
        IEnumerable<string> pendingMigrations =
            await dbContext.Database.GetPendingMigrationsAsync();

        // Assert
        Assert.Empty(pendingMigrations);
    }
}
