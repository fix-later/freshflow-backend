using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Infrastructure.Persistence;

/// <summary>
/// Registers shared persistence services: <see cref="AppDbContext"/> and the
/// <see cref="DomainEventDispatchInterceptor"/> that dispatches domain events post-commit.
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
            opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            opt.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        return services;
    }
}
