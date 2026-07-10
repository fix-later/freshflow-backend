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
    public async Task MarketCoordinateReader_InactiveMarket_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var marketId = Guid.NewGuid();
        await fixture.InsertMarketAsync(marketId, "Inactive", 10.123m, 106.456m, isActive: false);
        var sut = new MarketCoordinateReader(fixture.Context);

        var result = await sut.FindByIdAsync(marketId, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RestaurantCoordinateReader_DefaultAddress_ReturnsCoordinatesAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, "Pho Fresh");
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.111m, 106.222m, isDefault: true);
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.333m, 106.444m, isDefault: false);
        var sut = new RestaurantCoordinateReader(fixture.Context);

        var result = await sut.FindByRestaurantIdAsync(restaurantId, default);

        result.Should().NotBeNull();
        result!.RestaurantId.Should().Be(restaurantId);
        result.Name.Should().Be("Pho Fresh");
        result.Latitude.Should().Be(10.111m);
        result.Longitude.Should().Be(106.222m);
    }

    [Fact]
    public async Task RestaurantCoordinateReader_NonDefaultOrDeletedAddress_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, "Deleted Address");
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.111m, 106.222m, isDefault: false);
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.333m, 106.444m, isDefault: true, deleted: true);
        var sut = new RestaurantCoordinateReader(fixture.Context);

        var result = await sut.FindByRestaurantIdAsync(restaurantId, default);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("suspended")]
    public async Task RestaurantCoordinateReader_NonActiveStatus_ReturnsNullAsync(string status)
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var restaurantId = Guid.NewGuid();
        await fixture.InsertRestaurantAsync(restaurantId, "Not Active", status);
        await fixture.InsertDeliveryAddressAsync(restaurantId, 10.111m, 106.222m, isDefault: true);
        var sut = new RestaurantCoordinateReader(fixture.Context);

        var result = await sut.FindByRestaurantIdAsync(restaurantId, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DriverReader_ExistingUser_ReturnsRoleAndStatusAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await fixture.InsertRoleAsync(roleId, "driver");
        await fixture.InsertUserAsync(userId, roleId, isActive: true);
        var sut = new DriverReader(fixture.Context);

        var result = await sut.FindByUserIdAsync(userId, default);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.RoleName.Should().Be("driver");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DriverReader_DeletedUser_ReturnsNullAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await fixture.InsertRoleAsync(roleId, "driver");
        await fixture.InsertUserAsync(userId, roleId, isActive: true, deleted: true);
        var sut = new DriverReader(fixture.Context);

        var result = await sut.FindByUserIdAsync(userId, default);

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
                    "IsActive" INTEGER NOT NULL,
                    "DeletedAt" TEXT NULL
                );
                """);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE restaurants (
                    "Id" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    status TEXT NOT NULL
                );
                """);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE roles (
                    "Id" TEXT NOT NULL,
                    "Name" TEXT NOT NULL
                );
                """);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE users (
                    "Id" TEXT NOT NULL,
                    "RoleId" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
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
            bool isActive = true,
            bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO markets ("Id", "Name", "Latitude", "Longitude", "IsActive", "DeletedAt")
                VALUES ({id}, {name}, {latitude}, {longitude}, {isActive}, {DeletedAt(deleted)});
                """);

        public async Task InsertRestaurantAsync(Guid id, string name, string status = "active") =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO restaurants ("Id", "Name", status)
                VALUES ({id}, {name}, {status});
                """);

        public async Task InsertRoleAsync(Guid id, string name) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO roles ("Id", "Name")
                VALUES ({id}, {name});
                """);

        public async Task InsertUserAsync(
            Guid id,
            Guid roleId,
            bool isActive,
            bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO users ("Id", "RoleId", "IsActive", "DeletedAt")
                VALUES ({id}, {roleId}, {isActive}, {DeletedAt(deleted)});
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
