using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Behaviors;
using FreshFlow.Pricing.Application.Options;
using FreshFlow.Pricing.Infrastructure.Cache;
using FreshFlow.Pricing.Infrastructure.CrossModule;
using FreshFlow.Pricing.Infrastructure.Realtime;
using FreshFlow.Pricing.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FreshFlow.Pricing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPricingModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Pricing EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());

        // Pricing configuration options — bind from "Pricing" section via string indexer
        // (avoids a dependency on Microsoft.Extensions.Options.ConfigurationExtensions)
        services.Configure<PricingOptions>(options =>
        {
            var raw = config[$"{PricingOptions.SectionName}:{nameof(PricingOptions.MaxPriceVnd)}"];
            if (decimal.TryParse(
                    raw,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var max) && max > 0)
                options.MaxPriceVnd = max;
        });

        // MediatR — scan Application assembly for handlers.
        // Use a real type from that assembly so the reference is verified at compile time
        // (Assembly.Load with a string can fail silently at runtime if the name drifts).
        var applicationAssembly = typeof(IMarketProductRepository).Assembly;
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

        // Real-time broadcast (UC-PRI-08) — IHubContext<PricingHub> is registered by AddSignalR()
        services.AddScoped<IPricingBroadcastService, PricingBroadcastService>();

        // Redis price board (UC-PRI-07) — IConnectionMultiplexer is Singleton per StackExchange.Redis
        // best practices (connection pool is thread-safe and expensive to create).
        // AbortOnConnectFail is forced to false in code so the app survives Redis being
        // temporarily unavailable at startup, regardless of what the connection string says.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = config.GetConnectionString("Redis")
                ?? throw new InvalidOperationException(
                    "Missing required connection string 'Redis'. " +
                    "Set ConnectionStrings__Redis in environment or appsettings.json.");
            var opts = ConfigurationOptions.Parse(connectionString);
            opts.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(opts);
        });
        services.AddScoped<IPriceBoardCacheWriter, RedisPriceBoardCache>();

        // UC-PRI-09: live price board reader — DB-direct for v1.
        // TODO: swap DbPriceBoardReader for a Redis-backed reader when ready — see UC-PRI-07.
        services.AddScoped<IPriceBoardReader, DbPriceBoardReader>();

        return services;
    }
}
