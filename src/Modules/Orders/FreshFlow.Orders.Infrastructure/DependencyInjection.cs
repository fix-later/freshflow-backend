using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Behaviors;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Infrastructure.CrossModule;
using FreshFlow.Orders.Infrastructure.Documents;
using FreshFlow.Orders.Infrastructure.Jobs;
using FreshFlow.Orders.Infrastructure.Realtime;
using FreshFlow.Orders.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestPDF.Infrastructure;

namespace FreshFlow.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Orders EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);

        // QuestPDF (statement PDF export) — Community license, no native binary dependency.
        QuestPDF.Settings.License = LicenseType.Community;

        // MediatR — scan Application assembly for handlers.
        var applicationAssembly = typeof(IOrderRepository).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation — auto-register all validators from Application
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderIssueRepository, OrderIssueRepository>();
        services.AddScoped<IScheduledOrderRepository, ScheduledOrderRepository>();
        services.AddScoped<ICreditRepository, CreditRepository>();
        services.AddScoped<ICreditStatementRepository, CreditStatementRepository>();
        services.AddScoped<IOperationalSettingsRepository, OperationalSettingsRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();

        // Application services
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<ICreditStatementGenerationService, CreditStatementGenerationService>();
        services.AddScoped<IScheduledOrderGenerationService, ScheduledOrderGenerationService>();
        services.AddScoped<IOrderBroadcastService, OrderBroadcastService>();
        services.AddScoped<IStatementPdfRenderer, StatementPdfRenderer>();
        services.AddHostedService<ScheduledOrderGenerationHostedService>();
        services.AddHostedService<MonthlyCreditStatementHostedService>();

        // Cross-module read projections used by Orders without project references to Auth/Catalog/Pricing.
        services.AddScoped<IMarketProductReader, MarketProductReader>();
        services.AddScoped<IMarketProductImageReader, MarketProductImageReader>();
        services.AddScoped<IRestaurantReader, RestaurantReader>();
        services.AddScoped<IFavoriteReader, FavoriteReader>();

        return services;
    }
}
