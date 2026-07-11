using System.Reflection;
using FluentValidation;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Behaviors;
using FreshFlow.Hub.Infrastructure.CrossModule;
using FreshFlow.Hub.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Hub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHubModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Hub EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);

        var applicationAssembly = typeof(IHubRepository).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddScoped<IHubRepository, HubRepository>();
        services.AddScoped<IHubInboundRepository, HubInboundRepository>();
        services.AddScoped<IHubInventoryRepository, HubInventoryRepository>();
        services.AddScoped<IHubDiscrepancyRepository, HubDiscrepancyRepository>();
        services.AddScoped<IHubDiscrepancyReader, HubDiscrepancyRepository>();
        services.AddScoped<IOrderLookupReader, OrderLookupReader>();
        services.AddScoped<IDeliveryRouteReader, DeliveryRouteReader>();
        services.AddScoped<ICrossDockRepository, CrossDockRepository>();
        services.AddScoped<IHubOutboundRepository, HubOutboundRepository>();
        services.AddScoped<IHubHandoverRepository, HubHandoverRepository>();

        return services;
    }
}
