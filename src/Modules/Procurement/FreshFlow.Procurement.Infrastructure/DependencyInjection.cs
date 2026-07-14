using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Behaviors;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Infrastructure.CrossModule;
using FreshFlow.Procurement.Infrastructure.Jobs;
using FreshFlow.Procurement.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Procurement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProcurementModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);
        services.TryAddSingleton(TimeProvider.System);

        var applicationAssembly = typeof(IProcurementBatchRepository).Assembly;
        services.AddMediatR(mediatR =>
        {
            mediatR.RegisterServicesFromAssembly(applicationAssembly);
            mediatR.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddScoped<IConfirmedOrderReader, ConfirmedOrderReader>();
        services.AddScoped<IMarketProductMarketReader, MarketProductMarketReader>();
        services.AddScoped<IMarketAgentReader, MarketAgentReader>();
        services.AddScoped<IOperationalSettingsReader, OperationalSettingsReader>();
        services.AddScoped<IProcurementBatchRepository, ProcurementBatchRepository>();
        services.AddScoped<IProcurementBatchingService, BatchConfirmedOrdersService>();
        services.AddHostedService<ProcurementBatchingHostedService>();

        return services;
    }
}
