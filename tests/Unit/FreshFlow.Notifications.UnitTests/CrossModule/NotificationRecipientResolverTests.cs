using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Infrastructure;
using FreshFlow.Notifications.Infrastructure.CrossModule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.UnitTests.CrossModule;

[Trait("Category", "Unit")]
public sealed class NotificationRecipientResolverTests
{
    [Fact]
    public async Task ResolveUserIdByRestaurantIdAsync_ExistingRestaurant_ReturnsOwnerUserIdAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, userId);
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByRestaurantIdAsync(restaurantId, default);

        result.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveUserIdByRestaurantIdAsync_MissingRestaurant_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByRestaurantIdAsync(Guid.NewGuid(), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdByRestaurantIdAsync_EmptyRestaurantId_ReturnsNullWithoutQueryingAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByRestaurantIdAsync(Guid.Empty, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveRecipientByRestaurantIdAsync_ExistingRestaurant_ReturnsUserIdAndEmailAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, userId, "chef@example.com");
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveRecipientByRestaurantIdAsync(restaurantId, default);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Email.Should().Be("chef@example.com");
    }

    [Fact]
    public async Task ResolveRecipientByRestaurantIdAsync_MissingRestaurant_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveRecipientByRestaurantIdAsync(Guid.NewGuid(), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdByOrderIdAsync_ExistingOrder_ReturnsRestaurantOwnerUserIdAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, userId);
        await fixture.InsertOrderAsync(orderId, restaurantId);
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByOrderIdAsync(orderId, default);

        result.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveUserIdByOrderIdAsync_MissingOrder_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByOrderIdAsync(Guid.NewGuid(), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserIdByOrderIdAsync_EmptyOrderId_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new NotificationRecipientResolver(fixture.Context);

        var result = await sut.ResolveUserIdByOrderIdAsync(Guid.Empty, default);

        result.Should().BeNull();
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        private SqliteFixture(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE restaurants (
                    "Id" TEXT NOT NULL,
                    "UserId" TEXT NOT NULL,
                    status TEXT NULL
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE users (
                    "Id" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "DeletedAt" TEXT NULL
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE orders (
                    "Id" TEXT NOT NULL,
                    "RestaurantId" TEXT NOT NULL,
                    "deleted_at" TEXT NULL
                );
                """);

            return new SqliteFixture(connection, context);
        }

        public async Task InsertRestaurantAsync(Guid restaurantId, Guid userId, string email = "owner@example.com")
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO restaurants ("Id", "UserId", status)
                VALUES ({restaurantId}, {userId}, 'active');
                """);
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO users ("Id", "Email", "DeletedAt")
                VALUES ({userId}, {email}, NULL);
                """);
        }

        public async Task InsertOrderAsync(Guid orderId, Guid restaurantId, bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO orders ("Id", "RestaurantId", "deleted_at")
                VALUES ({orderId}, {restaurantId}, {DeletedAt(deleted)});
                """);

        private static string? DeletedAt(bool deleted) =>
            deleted ? DateTime.UtcNow.ToString("O") : null;

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
