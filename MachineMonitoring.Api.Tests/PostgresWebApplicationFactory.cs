using MachineMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MachineMonitoring.Api.Tests;

public sealed class PostgresWebApplicationFactory : CustomWebApplicationFactoryBase, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder(
        "postgres:18-alpine"
    )
        .WithDatabase("machine_monitoring_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _postgresContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _ = CreateClient();

        using IServiceScope scope = Services.CreateScope();

        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        await dbContext.Database.MigrateAsync();

        ProductionDatabaseSeeder seeder =
            scope.ServiceProvider.GetRequiredService<ProductionDatabaseSeeder>();

        await seeder.SeedAsync(CancellationToken.None);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        await _postgresContainer.DisposeAsync();
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveDbContext<MachineMonitoringDbContext>();

        services.AddDbContext<MachineMonitoringDbContext>(options =>
            options.UseNpgsql(ConnectionString)
        );
    }
}
