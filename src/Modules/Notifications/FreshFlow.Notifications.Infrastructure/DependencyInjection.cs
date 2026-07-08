using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        _ = config;

        // SCRUM-266 Option B: scan the Notifications application assembly for
        // integration-event consumers only. Persistence remains deferred.
        var applicationAssembly = System.Reflection.Assembly.Load("FreshFlow.Notifications.Application");
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));

        return services;
    }
}
