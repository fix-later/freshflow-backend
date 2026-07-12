using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryRouteRepositoryTests
{
    [Fact]
    public async Task AddAsync_AndFindByIdAsync_RoundTripStopsThroughJsonConversionAsync()
    {
        using var fixture = await SqliteRouteFixture.CreateAsync();
        var route = CreateRoute();
        route.ApplyOptimization(route.Stops, 12.34m, 25, 61700m, OptimizationCriteria.cost);

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
        result.TotalDistanceKm.Should().Be(12.34m);
        result.EstimatedDurationMinutes.Should().Be(25);
        result.EstimatedCost.Should().Be(61700m);
        result.OptimizationCriteria.Should().Be(OptimizationCriteria.cost);
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

    [Fact]
    public async Task ExistsOtherRouteForVehicleOnDateAsync_IgnoresExcludedCancelledDeletedAndDifferentDateAsync()
    {
        using var db = CreateInMemoryContext();
        var sut = new DeliveryRouteRepository(db);
        var vehicleId = Guid.NewGuid();
        var route = await AddRouteAsync(sut, db, DateTime.UtcNow);
        var sameDateRoute = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteAssignment(db, sameDateRoute, vehicleId, RouteStatus.assigned, route.ServiceDate);
        var cancelledRoute = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteAssignment(db, cancelledRoute, vehicleId, RouteStatus.cancelled, route.ServiceDate);
        var deletedRoute = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteAssignment(db, deletedRoute, vehicleId, RouteStatus.assigned, route.ServiceDate, deleted: true);
        var differentDateRoute = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteAssignment(db, differentDateRoute, vehicleId, RouteStatus.assigned, route.ServiceDate.AddDays(1));
        await db.SaveChangesAsync();

        var result = await sut.ExistsOtherRouteForVehicleOnDateAsync(vehicleId, route.ServiceDate, route.Id, default);
        var ignoredResult = await sut.ExistsOtherRouteForVehicleOnDateAsync(
            vehicleId,
            route.ServiceDate,
            sameDateRoute.Id,
            default);

        result.Should().BeTrue();
        ignoredResult.Should().BeFalse();
    }

    [Fact]
    public async Task GetByDriverAndDateAsync_FiltersDriverDateAndDeletedRoutesAsync()
    {
        using var db = CreateInMemoryContext();
        var sut = new DeliveryRouteRepository(db);
        var driverId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 7, 11);
        var matching = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteDriver(db, matching, driverId, serviceDate);
        var otherDriver = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteDriver(db, otherDriver, Guid.NewGuid(), serviceDate);
        var otherDate = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteDriver(db, otherDate, driverId, serviceDate.AddDays(1));
        var deleted = await AddRouteAsync(sut, db, DateTime.UtcNow);
        SetRouteDriver(db, deleted, driverId, serviceDate, deleted: true);
        await db.SaveChangesAsync();

        var result = await sut.GetByDriverAndDateAsync(driverId, serviceDate, default);

        result.Should().ContainSingle(route => route.Id == matching.Id);
    }

    [Fact]
    public async Task SaveAssignmentAsync_HappyPath_ReturnsTrueAsync()
    {
        using var db = CreateInMemoryContext();
        var sut = new DeliveryRouteRepository(db);
        var route = CreateReviewedRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        await sut.AddAsync(route, default);

        var result = await sut.SaveAssignmentAsync(default);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueViolation_PostgresUniqueViolation_ReturnsTrue()
    {
        var pgException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation);
        var dbUpdateException = new DbUpdateException("insert failed", pgException);

        var result = DeliveryRouteRepository.IsUniqueViolation(dbUpdateException);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueViolation_NonPostgresUniqueViolation_ReturnsFalse()
    {
        var dbUpdateException = new DbUpdateException("insert failed", new InvalidOperationException());

        var result = DeliveryRouteRepository.IsUniqueViolation(dbUpdateException);

        result.Should().BeFalse();
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

    private static void SetRouteAssignment(
        AppDbContext db,
        DeliveryRoute route,
        Guid vehicleId,
        RouteStatus status,
        DateOnly serviceDate,
        bool deleted = false)
    {
        db.Entry(route).Property(r => r.VehicleId).CurrentValue = vehicleId;
        db.Entry(route).Property(r => r.Status).CurrentValue = status;
        db.Entry(route).Property(r => r.ServiceDate).CurrentValue = serviceDate;
        if (deleted)
            db.Entry(route).Property(r => r.DeletedAt).CurrentValue = DateTime.UtcNow;
    }

    private static void SetRouteDriver(
        AppDbContext db,
        DeliveryRoute route,
        Guid driverId,
        DateOnly serviceDate,
        bool deleted = false)
    {
        db.Entry(route).Property(r => r.DriverUserId).CurrentValue = driverId;
        db.Entry(route).Property(r => r.Status).CurrentValue = RouteStatus.assigned;
        db.Entry(route).Property(r => r.ServiceDate).CurrentValue = serviceDate;
        if (deleted)
            db.Entry(route).Property(r => r.DeletedAt).CurrentValue = DateTime.UtcNow;
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);

    private static DeliveryRoute CreateReviewedRoute()
    {
        var route = CreateRoute();
        route.ApplyOptimization(route.Stops, 12.34m, 25, 61700m, OptimizationCriteria.cost);
        route.Select();
        route.MarkReviewed();
        return route;
    }

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
