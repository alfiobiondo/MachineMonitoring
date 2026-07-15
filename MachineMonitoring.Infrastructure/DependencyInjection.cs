using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMachineMonitoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString =
            configuration.GetConnectionString("MachineMonitoring")
            ?? throw new InvalidOperationException(
                "Connection string 'MachineMonitoring' was not found."
            );

        services.AddDbContext<MachineMonitoringDbContext>(options =>
            options.UseNpgsql(connectionString)
        );

        services.AddScoped<ProductionDatabaseSeeder>();

        services.AddScoped<IMaterialRepository, PostgresMaterialRepository>();

        services.AddScoped<INozzleRepository, PostgresNozzleRepository>();

        services.AddScoped<IDrawingFileRepository, PostgresDrawingFileRepository>();

        services.AddScoped<IMachineCapabilitiesRepository, PostgresMachineCapabilitiesRepository>();

        services.AddScoped<IMachineOperationRepository, PostgresMachineOperationRepository>();

        return services;
    }
}
