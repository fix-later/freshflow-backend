using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Behaviors;
using FreshFlow.Invoicing.Application.Services;
using FreshFlow.Invoicing.Infrastructure.CrossModule;
using FreshFlow.Invoicing.Infrastructure.Documents;
using FreshFlow.Invoicing.Infrastructure.Jobs;
using FreshFlow.Invoicing.Infrastructure.Persistence.Repositories;
using FreshFlow.Invoicing.Infrastructure.Provider;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestPDF.Infrastructure;

namespace FreshFlow.Invoicing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoicingModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Invoicing EF configurations + keyless Rows.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);
        services.TryAddSingleton(TimeProvider.System);
        QuestPDF.Settings.License = LicenseType.Community;

        var applicationAssembly = typeof(IInvoiceRepository).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // E-invoice provider (NCC). Stub for dev/sandbox; a real MISA/VNPT/Viettel implementation
        // registers here behind the same interface, selected by config, with no other code change.
        services.AddScoped<IEInvoiceProvider, StubEInvoiceProvider>();

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IOrderInvoiceReader, OrderInvoiceReader>();
        services.AddScoped<IRestaurantReader, RestaurantReader>();
        services.AddScoped<IInvoiceIssuanceService, InvoiceIssuanceService>();
        services.AddSingleton<InvoicePdfRenderer>();

        services.AddHostedService<InvoiceIssuanceRetryHostedService>();

        return services;
    }
}
