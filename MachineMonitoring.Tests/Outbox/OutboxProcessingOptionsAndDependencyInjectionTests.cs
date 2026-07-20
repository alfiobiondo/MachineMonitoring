using System.ComponentModel.DataAnnotations;
using MachineMonitoring.Infrastructure;
using MachineMonitoring.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Outbox;

public sealed class OutboxProcessingOptionsAndDependencyInjectionTests
{
    [Fact]
    public void ProductionInfrastructureDoesNotRegisterOutboxWorker()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MachineMonitoring"] =
                        "Host=localhost;Database=machine_monitoring;Username=test;Password=test",
                }
            )
            .Build();

        services.AddMachineMonitoringInfrastructure(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutboxProcessor)
        );
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutboxMessageDispatcher)
        );
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType == typeof(OutboxProcessingBackgroundService)
        );
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OutboxProcessingBackgroundService)
        );
    }

    [Fact]
    public void BatchSizeValidation()
    {
        ServiceProvider serviceProvider = BuildOptionsProvider(
            options =>
            {
                ForceSet(options, nameof(OutboxProcessingOptions.BatchSize), 0);
                ForceSet(options, nameof(OutboxProcessingOptions.PollingIntervalSeconds), 5);
            }
        );

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            _ = serviceProvider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value
        );

        Assert.Contains(nameof(OutboxProcessingOptions.BatchSize), exception.Message);
    }

    [Fact]
    public void PollingIntervalValidation()
    {
        ServiceProvider serviceProvider = BuildOptionsProvider(
            options =>
            {
                ForceSet(options, nameof(OutboxProcessingOptions.BatchSize), 100);
                ForceSet(options, nameof(OutboxProcessingOptions.PollingIntervalSeconds), 0);
            }
        );

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            _ = serviceProvider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value
        );

        Assert.Contains(nameof(OutboxProcessingOptions.PollingIntervalSeconds), exception.Message);
    }

    private static ServiceProvider BuildOptionsProvider(Action<OutboxProcessingOptions> configure)
    {
        ServiceCollection services = [];

        services
            .AddOptions<OutboxProcessingOptions>()
            .Configure(configure)
            .ValidateDataAnnotations();

        return services.BuildServiceProvider();
    }

    private static void ForceSet<T>(OutboxProcessingOptions options, string propertyName, T value)
    {
        typeof(OutboxProcessingOptions).GetProperty(propertyName)!.SetValue(options, value);
    }
}
