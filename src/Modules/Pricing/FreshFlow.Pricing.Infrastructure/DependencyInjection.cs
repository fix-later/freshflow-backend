using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Behaviors;
using FreshFlow.Pricing.Application.Options;
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
