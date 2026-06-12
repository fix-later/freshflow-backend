using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Behaviors;
using FreshFlow.Pricing.Infrastructure.CrossModule;
using FreshFlow.Pricing.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Pricing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPricingModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Pricing EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());

        // MediatR — scan Application assembly for handlers
        var applicationAssembly = Assembly.Load("FreshFlow.Pricing.Application");
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation — auto-register all validators from Application
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // Repositories
        services.AddScoped<IMarketProductRepository, MarketProductRepository>();
        services.AddScoped<IPriceSnapshotRepository, PriceSnapshotRepository>();

        // Cross-module read services
        services.AddScoped<IAssignedMarketReader, AssignedMarketReader>();
        services.AddScoped<IMarketProductReader, MarketProductReader>();

        return services;
    }
}
