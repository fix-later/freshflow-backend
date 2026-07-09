using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Behaviors;
using FreshFlow.Logistics.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Logistics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLogisticsModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Logistics EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);

        var applicationAssembly = typeof(IVehicleRepository).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDeliveryZoneRepository, DeliveryZoneRepository>();

        // Logistics:MaxStopsPerVehicle is intentionally left for SCRUM-306/307 route logic.
        return services;
    }
}
