using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.CrossModule;

[Trait("Category", "Unit")]
public sealed class RestaurantOwnerReaderTests
{
    [Fact]
    public async Task FindRestaurantIdByUserIdAsync_ExistingOwner_ReturnsRestaurantIdAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var userId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, userId);
        var sut = new RestaurantOwnerReader(fixture.Context);

        var result = await sut.FindRestaurantIdByUserIdAsync(userId, default);

        result.Should().Be(restaurantId);
    }

    [Fact]
    public async Task FindRestaurantIdByUserIdAsync_MissingOwner_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new RestaurantOwnerReader(fixture.Context);

        var result = await sut.FindRestaurantIdByUserIdAsync(Guid.NewGuid(), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindRestaurantIdByUserIdAsync_EmptyUserId_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var sut = new RestaurantOwnerReader(fixture.Context);

        var result = await sut.FindRestaurantIdByUserIdAsync(Guid.Empty, default);

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
            _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

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
                    "UserId" TEXT NOT NULL
                );
                """);

            return new SqliteFixture(connection, context);
        }

        public async Task InsertRestaurantAsync(Guid restaurantId, Guid userId) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO restaurants ("Id", "UserId")
                VALUES ({restaurantId}, {userId});
                """);

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
