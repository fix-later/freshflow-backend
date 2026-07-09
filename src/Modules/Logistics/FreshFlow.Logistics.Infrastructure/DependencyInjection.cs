using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Behaviors;
using FreshFlow.Logistics.Infrastructure.Configuration;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using FreshFlow.Logistics.Infrastructure.Optimization;
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
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IDriverReader, DriverReader>();
        services.AddScoped<IMarketCoordinateReader, MarketCoordinateReader>();
        services.AddScoped<IRestaurantCoordinateReader, RestaurantCoordinateReader>();
        services.AddScoped<IVehicleCapacityPolicy, VehicleCapacityPolicy>();
        services.AddScoped<IRouteOptimizer, NearestNeighborTwoOptOptimizer>();

        // VehicleCapacityPolicy uses Logistics:MaxStopsPerVehicle for SCRUM-306/307 route capacity checks.
        return services;
    }
}
