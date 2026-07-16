using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

internal static class DependencyInjectionExtensions
{
    public static IServiceCollection RemoveDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();

        services.RemoveAll<TContext>();

        return services;
    }
}
