using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Orders.UnitTests.Notifications;

[Trait("Category", "Unit")]
public sealed class CreditLimitThresholdNotificationStubTests
{
    [Fact]
    public void AddNotificationsModule_RegistersDomainEventNotificationConsumers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"notifications-{Guid.NewGuid()}"));
        services.AddNotificationsModule(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<INotificationHandler<CreditLimitThresholdReachedIntegrationEvent>>()
            .Should()
            .ContainSingle();
        provider
            .GetServices<INotificationHandler<OrderConfirmedIntegrationEvent>>()
            .Should()
            .ContainSingle();
        provider
            .GetServices<INotificationHandler<OrderCancelledIntegrationEvent>>()
            .Should()
            .ContainSingle();
    }
}
