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
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ProductionSequenceService>();
        services.AddScoped<MachineOperationStartCoordinator>();
        services.AddScoped<MachineRuntimeAssignedOperationQuery>();
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
