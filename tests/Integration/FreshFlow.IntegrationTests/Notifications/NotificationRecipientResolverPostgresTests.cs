using FluentAssertions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Notifications.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Notifications;

[Trait("Category", "Integration")]
public sealed class NotificationRecipientResolverPostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task ResolveRecipientByRestaurantIdAsync_PostgresProjection_ReturnsOwnerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = await db.Set<User>()
            .AsNoTracking()
            .SingleAsync(user => user.Email == "admin@test.freshflow");
        var restaurantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO restaurants ("Id", "Name", "UserId", "CreatedAt", "UpdatedAt")
            VALUES ({restaurantId}, {"Resolver PostgreSQL Test"}, {owner.Id}, {now}, {now})
            """);

        var resolver = scope.ServiceProvider.GetRequiredService<INotificationRecipientResolver>();
        var recipient = await resolver.ResolveRecipientByRestaurantIdAsync(restaurantId, default);

        recipient.Should().NotBeNull();
        recipient!.UserId.Should().Be(owner.Id);
        recipient.Email.Should().Be(owner.Email);
    }
}
