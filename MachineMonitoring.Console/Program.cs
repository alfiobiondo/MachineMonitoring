using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Console;
using MachineMonitoring.Infrastructure;
using MachineMonitoring.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

// builder
//     .Services.AddOptions<MachineOptions>()
//     .Bind(builder.Configuration.GetSection(MachineOptions.SectionName))
//     .ValidateDataAnnotations()
//     .Validate(options => options.Id.StartsWith("M-", StringComparison.OrdinalIgnoreCase))
//     .ValidateOnStart();

builder
    .Services.AddOptions<MachineDataOptions>()
    .Bind(builder.Configuration.GetSection(MachineDataOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<PollingOptions>()
    .Bind(builder.Configuration.GetSection(PollingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddTransient<IMachineProvider, JsonMachineProvider>();
builder.Services.AddTransient<MachineFormatter>();
builder.Services.AddTransient<MachineManager>();
builder.Services.AddTransient<MachineReporter>();
builder.Services.AddTransient<MachinePollingService>();
builder.Services.AddTransient<IMachineDiagnosticService, MachineDiagnosticService>();

builder.Services.AddHostedService<MachinePollingWorker>();

using IHost host = builder.Build();

ILogger logger = host
    .Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("MachineMonitoring.Console");

try
{
    await host.RunAsync();
}
catch (Exception exception)
{
    logger.LogCritical(exception, "The application terminated unexpectedly.");

    Console.WriteLine("An unexpected error occurred.");
}
