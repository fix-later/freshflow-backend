using System.Reflection;
using FluentValidation;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Behaviors;
using FreshFlow.Catalog.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using MediatR;
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

        // MediatR — scan Application assembly for handlers
        var applicationAssembly = Assembly.Load("FreshFlow.Catalog.Application");
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation — auto-register all validators from Application
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // Repositories
        services.AddScoped<IMarketRepository, MarketRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<IUnitOfMeasurementRepository, UnitOfMeasurementRepository>();
        services.AddScoped<IPackingCodeRepository, PackingCodeRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
