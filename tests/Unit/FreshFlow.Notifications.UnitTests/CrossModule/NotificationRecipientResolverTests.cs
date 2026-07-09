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

            return new SqliteFixture(connection, context);
        }

        public async Task InsertRestaurantAsync(Guid restaurantId, Guid userId) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO restaurants ("Id", "UserId", status)
                VALUES ({restaurantId}, {userId}, 'active');
                """);

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
