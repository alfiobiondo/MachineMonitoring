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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMachineOperationRepository>();

            services.AddSingleton(MachineOperationRepository);

            services.AddSingleton<IMachineOperationRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<TestMachineOperationRepository>()
            );
        });
    }
}
