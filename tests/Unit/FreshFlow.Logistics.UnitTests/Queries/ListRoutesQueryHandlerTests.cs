using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Queries.ListRoutes;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListRoutesQueryHandlerTests
{
    [Fact]
    public async Task Handle_MoreRowsThanPageSize_ReturnsNextCursorAndNextPageAsync()
    {
        using var db = CreateContext();
        var repository = new DeliveryRouteRepository(db);
        var sut = new ListRoutesQueryHandler(repository);
        var baseTime = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddRouteAsync(repository, db, baseTime.AddSeconds(3));
        var second = await AddRouteAsync(repository, db, baseTime.AddSeconds(2));
        await AddRouteAsync(repository, db, baseTime.AddSeconds(1));

        var firstPage = await sut.Handle(new ListRoutesQuery(null, PageSize: 1), default);
        var secondPage = await sut.Handle(new ListRoutesQuery(firstPage.Value.NextCursor, PageSize: 1), default);

        firstPage.IsSuccess.Should().BeTrue();
        firstPage.Value.Items.Should().ContainSingle(route => route.Id == newest.Id);
        firstPage.Value.NextCursor.Should().NotBeNull();
        secondPage.Value.Items.Should().ContainSingle(route => route.Id == second.Id);
        secondPage.Value.NextCursor.Should().NotBeNull();
        secondPage.Value.Items.Select(route => route.Id)
            .Should()
            .NotIntersectWith(firstPage.Value.Items.Select(route => route.Id));
    }

    [Fact]
    public async Task Handle_LastPage_ReturnsNullNextCursorAsync()
    {
        using var db = CreateContext();
        var repository = new DeliveryRouteRepository(db);
        var sut = new ListRoutesQueryHandler(repository);
        await AddRouteAsync(repository, db, DateTime.UtcNow);

        var result = await sut.Handle(new ListRoutesQuery(null, PageSize: 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FiltersByServiceDateAndStatusAsync()
    {
        using var db = CreateContext();
        var repository = new DeliveryRouteRepository(db);
        var sut = new ListRoutesQueryHandler(repository);
        var targetDate = new DateOnly(2026, 7, 9);
        var selected = await AddRouteAsync(repository, db, DateTime.UtcNow.AddSeconds(2), targetDate, select: true);
        await AddRouteAsync(repository, db, DateTime.UtcNow.AddSeconds(1), targetDate);
        await AddRouteAsync(repository, db, DateTime.UtcNow.AddSeconds(3), new DateOnly(2026, 7, 10), select: true);

        var result = await sut.Handle(
            new ListRoutesQuery(null, PageSize: 10, ServiceDate: targetDate, Status: "selected"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(route => route.Id == selected.Id);
        result.Value.Items[0].ServiceDate.Should().Be(targetDate);
        result.Value.Items[0].Status.Should().Be("selected");
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsValidationFailureAsync()
    {
        using var db = CreateContext();
        var repository = new DeliveryRouteRepository(db);
        var sut = new ListRoutesQueryHandler(repository);

        var result = await sut.Handle(new ListRoutesQuery(null, Status: "unknown"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-list-routes-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<DeliveryRoute> AddRouteAsync(
        DeliveryRouteRepository repository,
        AppDbContext db,
        DateTime createdAt,
        DateOnly? serviceDate = null,
        bool select = false)
    {
        var route = DeliveryRoute.CreateDirect(
            serviceDate ?? new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        if (select)
            route.Select();

        await repository.AddAsync(route, default);
        await repository.SaveChangesAsync(default);
        db.Entry(route).Property(r => r.CreatedAt).CurrentValue = createdAt;
        db.Entry(route).Property(r => r.UpdatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return route;
    }
}
