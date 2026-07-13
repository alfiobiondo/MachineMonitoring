using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Console;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure;
using MachineMonitoring.Infrastructure.Configuration;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

// Options: file JSON delle macchine
builder
    .Services.AddOptions<MachineDataOptions>()
    .Bind(builder.Configuration.GetSection(MachineDataOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Options: polling
builder
    .Services.AddOptions<PollingOptions>()
    .Bind(builder.Configuration.GetSection(PollingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Options: limite di concorrenza
builder
    .Services.AddOptions<DiagnosticOptions>()
    .Bind(builder.Configuration.GetSection(DiagnosticOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Options: retry
builder
    .Services.AddOptions<DiagnosticRetryOptions>()
    .Bind(builder.Configuration.GetSection(DiagnosticRetryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Options: cache
builder
    .Services.AddOptions<DiagnosticCacheOptions>()
    .Bind(builder.Configuration.GetSection(DiagnosticCacheOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddMemoryCache();

// Monitoraggio macchine
builder.Services.AddTransient<IMachineProvider, JsonMachineProvider>();

builder.Services.AddTransient<MachineFormatter>();
builder.Services.AddTransient<MachineManager>();
builder.Services.AddTransient<MachineReporter>();
builder.Services.AddTransient<MachinePollingService>();

builder.Services.AddHostedService<MachinePollingWorker>();

// Pipeline diagnostica:
// cache → retry → limite concorrenza → servizio reale
builder.Services.AddSingleton<IRawMachineDiagnosticService, MachineDiagnosticService>();

builder.Services.AddSingleton<
    ILimitedMachineDiagnosticService,
    LimitedConcurrencyMachineDiagnosticService
>();

builder.Services.AddSingleton<
    IRetryingMachineDiagnosticService,
    RetryingMachineDiagnosticService
>();

builder.Services.AddSingleton<IMachineDiagnosticService, CachedMachineDiagnosticService>();

// Catalogo produttivo in memoria
builder.Services.AddSingleton<InMemoryProductionCatalog>();

// Repository produttivi in memoria
builder.Services.AddSingleton<IMaterialRepository, InMemoryMaterialRepository>();

builder.Services.AddSingleton<INozzleRepository, InMemoryNozzleRepository>();

builder.Services.AddSingleton<IDrawingFileRepository, InMemoryDrawingFileRepository>();

builder.Services.AddSingleton<
    IMachineCapabilitiesRepository,
    InMemoryMachineCapabilitiesRepository
>();

builder.Services.AddSingleton<IMachineOperationRepository, InMemoryMachineOperationRepository>();

// Dominio e application service produttivo
builder.Services.AddSingleton<LaserCutConfigurationValidator>();

builder.Services.AddSingleton<MachineOperationApplicationService>();

// Deve essere registrato PRIMA di Build()
builder.Services.AddTransient<ProductionDemoService>();

using IHost host = builder.Build();

ILogger logger = host
    .Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("MachineMonitoring.Console");

try
{
    // Esegue una volta la dimostrazione produttiva
    using (IServiceScope scope = host.Services.CreateScope())
    {
        ProductionDemoService demoService =
            scope.ServiceProvider.GetRequiredService<ProductionDemoService>();

        await demoService.RunAsync(CancellationToken.None);
    }

    // Avvia il Generic Host e il worker
    await host.RunAsync();
}
catch (OperationCanceledException)
{
    logger.LogInformation("The application was stopped.");
}
catch (Exception exception)
{
    logger.LogCritical(exception, "The application terminated unexpectedly.");

    Console.WriteLine("An unexpected error occurred.");
}
