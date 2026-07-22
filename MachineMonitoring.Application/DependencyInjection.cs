using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Domain.Technology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IIncidentRandomSource, RandomIncidentRandomSource>();
        services.AddSingleton<
            IValidateOptions<MachineIncidentSimulatorOptions>,
            MachineIncidentSimulatorOptionsValidator
        >();
        services.AddSingleton<MachineIncidentCooldownTracker>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ProductionSequenceService>();
        services.AddScoped<MachineOperationStartCoordinator>();
        services.AddScoped<MachineRuntimeAssignedOperationQuery>();
        services.AddScoped<MachineOperationApplicationService>();
        services.AddScoped<MachineOperationWarningApplicationService>();
        services.AddScoped<MachineIncidentSimulator>();
        services.AddScoped<MachineOperationEventApplicationService>();
        services.AddScoped<MachineAlarmApplicationService>();
        services.AddScoped<MachineRuntimeApplicationService>();
        services.AddScoped<WorkpieceApplicationService>();
        services.AddScoped<ProductionLotApplicationService>();

        services.AddScoped<MachineOperationSimulator>();

        return services;
    }
}
