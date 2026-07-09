using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.CrossModule;

[Trait("Category", "Unit")]
public sealed class CoordinateReaderTests
{
    [Fact]
    public async Task MarketCoordinateReader_ExistingMarket_ReturnsCoordinatesAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var marketId = Guid.NewGuid();
        await fixture.InsertMarketAsync(marketId, "Central Market", 10.123m, 106.456m);
        var sut = new MarketCoordinateReader(fixture.Context);

        var result = await sut.FindByIdAsync(marketId, default);

        result.Should().NotBeNull();
        result!.Id.Should().Be(marketId);
        result.Name.Should().Be("Central Market");
        result.Latitude.Should().Be(10.123m);
        result.Longitude.Should().Be(106.456m);
    }

    [Fact]
    public async Task MarketCoordinateReader_DeletedMarket_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var marketId = Guid.NewGuid();
        await fixture.InsertMarketAsync(marketId, "Deleted", 10.123m, 106.456m, deleted: true);
        var sut = new MarketCoordinateReader(fixture.Context);

        var result = await sut.FindByIdAsync(marketId, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RestaurantCoordinateReader_DefaultAddress_ReturnsCoordinatesAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.111m, 106.222m, isDefault: true);
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.333m, 106.444m, isDefault: false);
        var sut = new RestaurantCoordinateReader(fixture.Context);

        var result = await sut.FindByRestaurantIdAsync(restaurantId, default);

        result.Should().NotBeNull();
        result!.RestaurantId.Should().Be(restaurantId);
        result.Latitude.Should().Be(10.111m);
        result.Longitude.Should().Be(106.222m);
    }

    [Fact]
    public async Task RestaurantCoordinateReader_NonDefaultOrDeletedAddress_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.111m, 106.222m, isDefault: false);
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.333m, 106.444m, isDefault: true, deleted: true);
        var sut = new RestaurantCoordinateReader(fixture.Context);

        var result = await sut.FindByRestaurantIdAsync(restaurantId, default);

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
            SQLitePCL.Batteries.Init();
            _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE markets (
                    "Id" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Latitude" TEXT NULL,
                    "Longitude" TEXT NULL,
                    "DeletedAt" TEXT NULL
                );
                """);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE delivery_addresses (
                    "RestaurantId" TEXT NOT NULL,
                    "Latitude" TEXT NULL,
                    "Longitude" TEXT NULL,
                    "IsDefault" INTEGER NOT NULL,
                    "DeletedAt" TEXT NULL
                );
                """);

            return new SqliteFixture(connection, context);
        }

        public async Task InsertMarketAsync(
            Guid id,
            string name,
            decimal? latitude,
            decimal? longitude,
            bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO markets ("Id", "Name", "Latitude", "Longitude", "DeletedAt")
                VALUES ({id}, {name}, {latitude}, {longitude}, {DeletedAt(deleted)});
                """);

        public async Task InsertDeliveryAddressAsync(
            Guid restaurantId,
            decimal? latitude,
            decimal? longitude,
            bool isDefault,
            bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO delivery_addresses ("RestaurantId", "Latitude", "Longitude", "IsDefault", "DeletedAt")
                VALUES ({restaurantId}, {latitude}, {longitude}, {isDefault}, {DeletedAt(deleted)});
                """);

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }

        private static string? DeletedAt(bool deleted) =>
            deleted ? DateTime.UtcNow.ToString("O") : null;
    }
}
