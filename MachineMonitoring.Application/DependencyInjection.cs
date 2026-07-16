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

        services.AddScoped<ProductionSequenceService>();
        services.AddScoped<MachineOperationApplicationService>();
        services.AddScoped<WorkpieceApplicationService>();
        services.AddScoped<ProductionLotApplicationService>();

        services.AddScoped<MachineOperationSimulator>();

        return services;
    }
}
