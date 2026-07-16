using MachineMonitoring.Api.Tests.Fakes;
using MachineMonitoring.Application.Production.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestMachineOperationRepository MachineOperationRepository { get; } = new();
    public TestProductionCatalog ProductionCatalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMachineOperationRepository>();
            services.RemoveAll<IMaterialRepository>();
            services.RemoveAll<INozzleRepository>();
            services.RemoveAll<IDrawingFileRepository>();
            services.RemoveAll<IMachineCapabilitiesRepository>();

            services.AddSingleton(MachineOperationRepository);
            services.AddSingleton(ProductionCatalog);

            services.AddSingleton<IMachineOperationRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestMachineOperationRepository>()
            );

            services.AddSingleton<IMaterialRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestProductionCatalog>()
            );

            services.AddSingleton<INozzleRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestProductionCatalog>()
            );

            services.AddSingleton<IDrawingFileRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestProductionCatalog>()
            );

            services.AddSingleton<IMachineCapabilitiesRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestProductionCatalog>()
            );
        });
    }
}
