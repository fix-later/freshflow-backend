using FluentValidation;
using FreshFlow.Infrastructure.Persistence.Behaviors;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Infrastructure.Persistence;

/// <summary>
/// Registers shared persistence services: <see cref="AppDbContext"/>, the
/// <see cref="DomainEventDispatchInterceptor"/> that dispatches domain events post-commit,
/// and the audit log writer + its integration-event consumers (SCRUM-359b) — audit isn't
/// owned by any single module, so it's registered here rather than in a module's DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DomainEventDispatchInterceptor is scoped so it receives the scoped IPublisher.
        // The DbContext factory overload resolves it per-scope from the app's DI container.
        services.AddScoped<DomainEventDispatchInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opt) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException(
                    "Missing required connection string 'DefaultConnection'. " +
                    "Set ConnectionStrings__DefaultConnection in environment or appsettings.json.");

            opt.UseNpgsql(connectionString);
            opt.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }
}
