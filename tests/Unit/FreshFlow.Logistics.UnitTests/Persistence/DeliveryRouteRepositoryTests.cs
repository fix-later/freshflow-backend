using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryRouteRepositoryTests
{
    [Fact]
    public async Task AddAsync_AndFindByIdAsync_RoundTripStopsThroughJsonConversionAsync()
    {
        using var fixture = await SqliteRouteFixture.CreateAsync();
        var route = CreateRoute();

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new DeliveryRouteRepository(writeContext);
            await repository.AddAsync(route, default);
            await repository.SaveChangesAsync(default);
        }

        await using var readContext = fixture.CreateContext();
        var sut = new DeliveryRouteRepository(readContext);

        var result = await sut.FindByIdAsync(route.Id, default);

        result.Should().NotBeNull();
        result!.Stops.Should().Equal(route.Stops);
        result.Stops[0].EntityName.Should().Be("Market");
        result.Stops[1].EntityName.Should().Be("Restaurant");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageAsync_InvalidPageSize_ThrowsArgumentExceptionAsync(int pageSize)
    {
        using var db = CreateInMemoryContext();
        var sut = new DeliveryRouteRepository(db);
        Func<Task> act = () => sut.GetPageAsync(null, pageSize, null, null, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPageAsync_MalformedCursor_IgnoresCursorAsync()
    {
        using var db = CreateInMemoryContext();
        var sut = new DeliveryRouteRepository(db);
        var newest = await AddRouteAsync(sut, db, DateTime.UtcNow.AddSeconds(2));
        await AddRouteAsync(sut, db, DateTime.UtcNow.AddSeconds(1));

        var result = await sut.GetPageAsync("not-valid-base64", 1, null, null, default);

        result.Items.Should().ContainSingle(route => route.Id == newest.Id);
    }

    private static AppDbContext CreateInMemoryContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-route-repository-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<DeliveryRoute> AddRouteAsync(
        DeliveryRouteRepository repository,
        AppDbContext db,
        DateTime createdAt)
    {
        var route = CreateRoute();
        await repository.AddAsync(route, default);
        await repository.SaveChangesAsync(default);
        db.Entry(route).Property(r => r.CreatedAt).CurrentValue = createdAt;
        db.Entry(route).Property(r => r.UpdatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return route;
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);

    private sealed class SqliteRouteFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        private SqliteRouteFixture(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<SqliteRouteFixture> CreateAsync()
        {
            SQLitePCL.Batteries.Init();
            _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var fixture = new SqliteRouteFixture(connection);
            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync();

            return fixture;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new AppDbContext(options);
        }

        public void Dispose() => _connection.Dispose();
    }
}
