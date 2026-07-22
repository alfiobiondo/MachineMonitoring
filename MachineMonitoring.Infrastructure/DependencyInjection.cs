using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Infrastructure.HealthChecks;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Outbox;
using MachineMonitoring.Infrastructure.Persistence.Queries;
using MachineMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

        services.AddScoped<ScopedProductionNotificationCollector>();
        services.AddScoped<IProductionNotificationCollector>(serviceProvider =>
            serviceProvider.GetRequiredService<ScopedProductionNotificationCollector>()
        );
        services.AddScoped<IProductionNotificationPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<ScopedProductionNotificationCollector>()
        );
        services.AddSingleton<ProductionNotificationOutboxSerializer>();

        services.AddSingleton<OutboxWakeUpSignal>();

        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        services
            .AddOptions<OutboxProcessingOptions>()
            .Bind(configuration.GetSection(OutboxProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>(
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "database"]
            );

        services.AddScoped<ProductionDatabaseSeeder>();
        services.AddScoped<IProductionTransactionManager, EfCoreProductionTransactionManager>();
        services.AddScoped<ILiveSnapshotQuery, PostgresLiveSnapshotQuery>();
        services.AddScoped<IProductionLotRepository, PostgresProductionLotRepository>();
        services.AddScoped<IWorkpieceRepository, PostgresWorkpieceRepository>();
        services.AddScoped<
            IMachineOperationEventRepository,
            PostgresMachineOperationEventRepository
        >();
        services.AddScoped<IMachineAlarmRepository, PostgresMachineAlarmRepository>();
        services.AddScoped<IMachineRuntimeStateRepository, PostgresMachineRuntimeStateRepository>();

        services.AddScoped<IMaterialRepository, PostgresMaterialRepository>();

        services.AddScoped<INozzleRepository, PostgresNozzleRepository>();

        services.AddScoped<IDrawingFileRepository, PostgresDrawingFileRepository>();

        services.AddScoped<IMachineCapabilitiesRepository, PostgresMachineCapabilitiesRepository>();

        services.AddScoped<IMachineOperationRepository, PostgresMachineOperationRepository>();

        return services;
    }
}
