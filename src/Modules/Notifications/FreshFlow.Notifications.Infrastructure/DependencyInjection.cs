using System.Reflection;
using FluentValidation;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Behaviors;
using FreshFlow.Notifications.Application.Services;
using FreshFlow.Notifications.Infrastructure.CrossModule;
using FreshFlow.Notifications.Infrastructure.Email;
using FreshFlow.Notifications.Infrastructure.Jobs;
using FreshFlow.Notifications.Infrastructure.Push;
using FreshFlow.Notifications.Infrastructure.Realtime;
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
        services.AddScoped<INotificationBroadcastService, NotificationBroadcastService>();

        var pushSection = config.GetSection("Notifications:Push");
        var pushOptions = new ExpoPushOptions
        {
            Enabled = bool.TryParse(pushSection["Enabled"], out var pushEnabled) && pushEnabled,
            BaseUrl = pushSection["BaseUrl"]
                ?? "https://exp.host/--/api/v2/push/send",
            AccessToken = pushSection["AccessToken"],
            TimeoutSeconds = int.TryParse(pushSection["TimeoutSeconds"], out var timeoutSeconds)
                ? timeoutSeconds
                : 10,
        };
        if (pushOptions.Enabled)
        {
            if (!Uri.TryCreate(pushOptions.BaseUrl, UriKind.Absolute, out _)
                || pushOptions.TimeoutSeconds <= 0)
            {
                throw new InvalidOperationException(
                    "Notifications:Push requires an absolute BaseUrl and positive TimeoutSeconds.");
            }

            services.AddSingleton(pushOptions);
            services.AddHttpClient<IPushSender, ExpoPushSender>(client =>
                client.Timeout = TimeSpan.FromSeconds(pushOptions.TimeoutSeconds));
        }
        else
        {
            services.AddScoped<IPushSender, LogPushSender>();
        }

        // Email sender — real SMTP when configured (Notifications:Email:Smtp), otherwise a
        // log-only fallback so the app runs without a mail server (SCRUM-269).
        var smtpSection = config.GetSection("Notifications:Email:Smtp");
        var smtpOptions = new SmtpEmailOptions
        {
            Host = smtpSection["Host"],
            Port = int.TryParse(smtpSection["Port"], out var port) ? port : 587,
            EnableSsl = !bool.TryParse(smtpSection["EnableSsl"], out var ssl) || ssl,
            FromAddress = smtpSection["FromAddress"],
            Username = smtpSection["Username"],
            Password = smtpSection["Password"],
        };
        if (smtpOptions.IsConfigured)
        {
            services.AddSingleton(smtpOptions);
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LogEmailSender>();
        }
        services.AddScoped<INotificationRetryService, NotificationRetryService>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddHostedService<NotificationRetryHostedService>();

        return services;
    }
}
