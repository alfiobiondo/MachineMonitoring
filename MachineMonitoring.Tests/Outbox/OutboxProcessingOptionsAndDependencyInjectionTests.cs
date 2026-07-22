using System.ComponentModel.DataAnnotations;
using MachineMonitoring.Application;
using MachineMonitoring.Application.Production;
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
        public void ProductionInfrastructureRegistersOutboxProcessingServicesButNotHostedWorkerOrDispatcher()
        {
            ServiceCollection services = [];
            IConfiguration configuration = CreateConfiguration();

            services.AddMachineMonitoringInfrastructure(configuration);

            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(IOutboxProcessor)
            );
            Assert.DoesNotContain(
                services,
                descriptor => descriptor.ServiceType == typeof(IOutboxMessageDispatcher)
            );
            Assert.Contains(
                services,
                descriptor => descriptor.ServiceType == typeof(OutboxWakeUpSignal)
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
        public void ApiCompositionRegistersSingleOutboxHostedWorkerAndResolvesDispatcherAndProcessor()
        {
            ServiceCollection services = [];
            IConfiguration configuration = CreateConfiguration();

            services.AddMachineMonitoringApplication().AddMachineMonitoringInfrastructure(configuration);
            services.AddScoped<IOutboxMessageDispatcher, RecordingOutboxMessageDispatcher>();
            services.AddHostedService<OutboxProcessingBackgroundService>();

            Assert.Single(
                services,
                descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(OutboxProcessingBackgroundService)
            );

            using ServiceProvider serviceProvider = services.BuildServiceProvider(
                validateScopes: true
            );
            using IServiceScope scope = serviceProvider.CreateScope();

            Assert.IsType<RecordingOutboxMessageDispatcher>(
                scope.ServiceProvider.GetRequiredService<IOutboxMessageDispatcher>()
            );
            Assert.IsType<OutboxProcessor>(
                scope.ServiceProvider.GetRequiredService<IOutboxProcessor>()
            );
        }

        [Fact]
        public void ConsoleCompositionCanWriteOutboxWithoutDispatcherOrHostedWorker()
        {
            ServiceCollection services = [];
            IConfiguration configuration = CreateConfiguration();

            services.AddMachineMonitoringApplication().AddMachineMonitoringInfrastructure(configuration);

            Assert.DoesNotContain(
                services,
                descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(OutboxProcessingBackgroundService)
            );
            Assert.DoesNotContain(
                services,
                descriptor => descriptor.ServiceType == typeof(IOutboxMessageDispatcher)
            );

            using ServiceProvider serviceProvider = services.BuildServiceProvider(
                validateScopes: true
            );
            using IServiceScope scope = serviceProvider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductionNotificationPublisher>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductionNotificationCollector>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductionTransactionManager>());
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

        private static IConfiguration CreateConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MachineMonitoring"] =
                            "Host=localhost;Database=machine_monitoring;Username=test;Password=test",
                    }
                )
                .Build();
        }

        private static void ForceSet<T>(OutboxProcessingOptions options, string propertyName, T value)
        {
            typeof(OutboxProcessingOptions).GetProperty(propertyName)!.SetValue(options, value);
        }

        private sealed class RecordingOutboxMessageDispatcher : IOutboxMessageDispatcher
        {
            public Task DispatchAsync(
                OutboxDispatchMessage message,
                CancellationToken cancellationToken
            )
            {
                return Task.CompletedTask;
            }
        }
    }
