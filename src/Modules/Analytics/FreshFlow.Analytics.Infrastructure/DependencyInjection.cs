using System.Reflection;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Behaviors;
using FreshFlow.Analytics.Infrastructure.CrossModule;
using FreshFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);
        services.TryAddSingleton(TimeProvider.System);

        var applicationAssembly = typeof(IDashboardOverviewReader).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        services.AddScoped<IDashboardOverviewReader, DashboardOverviewReader>();
        services.AddScoped<IPriceTrendReader, PriceTrendReader>();
        services.AddScoped<IOrderMetricsReader, OrderMetricsReader>();
        services.AddScoped<IProcurementMetricsReader, ProcurementMetricsReader>();

        return services;
    }
}

