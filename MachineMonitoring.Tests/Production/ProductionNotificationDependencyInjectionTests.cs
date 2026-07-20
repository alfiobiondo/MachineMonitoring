using MachineMonitoring.Application;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionNotificationDependencyInjectionTests
{
    [Fact]
    public void ScopedPublisherAndCollector_ResolveSameInstance()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:MachineMonitoring"] = "Host=localhost;Database=test;Username=test;Password=test" }
            )
            .Build();

        services.AddMachineMonitoringApplication().AddMachineMonitoringInfrastructure(configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();

        IProductionNotificationPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationPublisher>();
        IProductionNotificationCollector collector =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationCollector>();

        Assert.Same(publisher, collector);
    }
}
