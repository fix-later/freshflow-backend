using System.Reflection;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Catalog EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());

        return services;
    }
}
