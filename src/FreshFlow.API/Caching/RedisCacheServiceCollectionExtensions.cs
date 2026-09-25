using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace FreshFlow.API.Caching;

/// <summary>
/// Wires the shared Redis + HybridCache infrastructure. Lives in the host layer (not
/// SharedKernel) because Domain projects may only depend on SharedKernel — pulling the
/// Redis package down there would drag it into every module's Domain (see CLAUDE.md
/// dependency rules). The host is the one place allowed to wire cross-module DI.
/// </summary>
public static class RedisCacheServiceCollectionExtensions
{
    private const bool DefaultCacheEnabled = true;
    private const int DefaultTtlMinutes = 5;

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration config)
    {
        // IConnectionMultiplexer — moved here from Pricing.Infrastructure.AddPricingModule so
        // any module can depend on Redis without depending on Pricing. Registration logic
        // (connection string, AbortOnConnectFail=false, error message) is unchanged: the
        // Pricing price board (RedisPriceBoardCache/Reader) still resolves this singleton and
        // is unaffected by Cache:Enabled below — that flag only gates the new HybridCache path.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = config.GetConnectionString("Redis")
                ?? throw new InvalidOperationException(
                    "Missing required connection string 'Redis'. " +
                    "Set ConnectionStrings__Redis in environment or appsettings.json.");
            var opts = ConfigurationOptions.Parse(connectionString);
            opts.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(opts);
        });

        var cacheEnabled = config.GetValue("Cache:Enabled", DefaultCacheEnabled);
        if (!cacheEnabled)
        {
            // Bypass entirely: no L2 Redis registration, no HybridCache in-memory L1 either.
            services.AddSingleton<HybridCache, NoOpHybridCache>();
            return services;
        }

        // L2 reuses the singleton above instead of opening a second connection pool. That keeps
        // the in-code AbortOnConnectFail=false hardening (a connection string missing
        // abortConnect=false would otherwise make L2 throw on connect), and means anything that
        // swaps IConnectionMultiplexer — the integration test factories mock it — swaps L2 too.
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, multiplexer) =>
                options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));

        var defaultTtlMinutes = config.GetValue("Cache:DefaultTtlMinutes", DefaultTtlMinutes);
        services.AddHybridCache(options =>
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(defaultTtlMinutes)
            });

        return services;
    }
}
