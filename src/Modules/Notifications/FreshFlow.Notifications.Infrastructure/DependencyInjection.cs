using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Behaviors;
using FreshFlow.Notifications.Application.Services;
using FreshFlow.Notifications.Infrastructure.CrossModule;
using FreshFlow.Notifications.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Register this assembly so AppDbContext discovers Notifications EF configurations.
        EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly());
        services.TryAddSingleton(config);

        // MediatR - scan Application assembly for command handlers and integration consumers.
        var applicationAssembly = typeof(INotificationDeviceRepository).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation - auto-register all validators from Application.
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationDeviceRepository, NotificationDeviceRepository>();

        // Application services and cross-module read projections
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();

        return services;
    }
}
