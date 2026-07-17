using MachineMonitoring.Application.Production;
using MachineMonitoring.Domain.Technology;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMachineMonitoringApplication(
        this IServiceCollection services
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LaserCutConfigurationValidator>();
        services.AddSingleton<IOperationProgressStrategy, RandomOperationProgressStrategy>();
        services.AddSingleton<IOperationFaultStrategy, RandomOperationFaultStrategy>();
        services.AddSingleton<IMachineFaultStrategy, RandomMachineFaultStrategy>();
        services.AddSingleton<IBufferedProductionNotificationPublisher, NoOpProductionNotificationPublisher>();
        services.AddSingleton<IProductionNotificationPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<IBufferedProductionNotificationPublisher>()
        );

        services.AddScoped<ProductionSequenceService>();
        services.AddScoped<MachineOperationApplicationService>();
        services.AddScoped<MachineOperationEventApplicationService>();
        services.AddScoped<MachineAlarmApplicationService>();
        services.AddScoped<MachineRuntimeApplicationService>();
        services.AddScoped<WorkpieceApplicationService>();
        services.AddScoped<ProductionLotApplicationService>();

        services.AddScoped<MachineOperationSimulator>();

        return services;
    }
}
