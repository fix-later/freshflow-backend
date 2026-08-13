using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Behaviors;
using FreshFlow.Logistics.Application.Common;
using FreshFlow.Logistics.Infrastructure.Configuration;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using FreshFlow.Logistics.Infrastructure.Optimization;
using FreshFlow.Logistics.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure.Realtime;
using FreshFlow.Logistics.Infrastructure.Repositories;
using FreshFlow.Logistics.Infrastructure.Routing;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;

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
        services.TryAddSingleton(TimeProvider.System);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDeliveryRouteRepository, DeliveryRouteRepository>();
        services.AddScoped<IRoutePlanRepository, RoutePlanRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDeliveryIssueRepository, DeliveryIssueRepository>();
        services.AddScoped<IDeliveryBroadcastService, DeliveryBroadcastService>();
        services.AddScoped<IDriverReader, DriverReader>();
        services.AddScoped<IHubDiscrepancyStatusReader, HubDiscrepancyStatusReader>();
        services.AddScoped<IHubSortingStateReader, HubSortingStateReader>();
        services.AddScoped<IHubCoordinateReader, HubCoordinateReader>();
        services.AddScoped<IOrderMarketReader, OrderMarketReader>();
        services.AddScoped<IOrderPackingReader, OrderPackingReader>();
        services.AddScoped<IOrderStatusReader, OrderStatusReader>();
        services.AddScoped<IRestaurantOwnerReader, RestaurantOwnerReader>();
        services.AddScoped<IMarketCoordinateReader, MarketCoordinateReader>();
        services.AddScoped<IRestaurantCoordinateReader, RestaurantCoordinateReader>();
        services.AddScoped<IVehicleCapacityPolicy, VehicleCapacityPolicy>();
        services.AddScoped<IRoutePlanningInputBuilder, RoutePlanningInputBuilder>();
        services.AddScoped<IMarketSessionVehicleReader, MarketSessionVehicleReader>();
        services.AddScoped<GoongRouteMatrixProvider>();
        services.AddScoped<IRouteMatrixCacheStore, RouteMatrixCacheStore>();
        services.AddScoped<IRouteMatrixProvider>(sp => new CachingRouteMatrixProvider(
            sp.GetRequiredService<GoongRouteMatrixProvider>(),
            sp.GetRequiredService<IRouteMatrixCacheStore>(),
            sp.GetRequiredService<IVehicleCapacityPolicy>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachingRouteMatrixProvider>>()));
        services.AddScoped<IRoutePlanningSolver, OrToolsRoutePlanningSolver>();
        services.AddScoped<IRouteOptimizer, RoadRouteOptimizer>();

        services.AddHttpClient(GoongRouteMatrixProvider.HttpClientName, client =>
            {
                client.BaseAddress = new Uri((config["Delivery:Goong:BaseUrl"] ?? "https://rsapi.goong.io").TrimEnd('/') + '/');
                client.Timeout = TimeSpan.FromSeconds(config.GetValue<int?>("Delivery:Goong:TimeoutSeconds") ?? 10);
            })
            .AddStandardResilienceHandler(options =>
                options.Retry.MaxRetryAttempts = config.GetValue<int?>("Delivery:Goong:RetryCount") ?? 2);

        // VehicleCapacityPolicy uses Logistics:MaxStopsPerVehicle for SCRUM-306/307 route capacity checks.
        return services;
    }
}
