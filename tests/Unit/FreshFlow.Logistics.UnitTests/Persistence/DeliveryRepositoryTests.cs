using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryRepositoryTests
{
    [Fact]
    public async Task AddRangeAsync_AndExistsForOrderAsync_PersistsDeliveriesAsync()
    {
        using var db = CreateContext();
        var sut = new DeliveryRepository(db);
        var orderId = Guid.NewGuid();
        var deliveries = new[]
        {
            Delivery.Create(Guid.NewGuid(), orderId, 1),
            Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 2)
        };

        await sut.AddRangeAsync(deliveries, default);
        await sut.SaveChangesAsync(default);

        (await sut.ExistsForOrderAsync(orderId, default)).Should().BeTrue();
        (await sut.ExistsForOrderAsync(Guid.NewGuid(), default)).Should().BeFalse();
    }

    [Fact]
    public async Task GetByRouteIdsAsync_ReturnsOnlyMatchingDeliveriesAsync()
    {
        using var db = CreateContext();
        var sut = new DeliveryRepository(db);
        var firstRouteId = Guid.NewGuid();
        var secondRouteId = Guid.NewGuid();
        var ignoredRouteId = Guid.NewGuid();
        var first = Delivery.Create(firstRouteId, Guid.NewGuid(), 2);
        var second = Delivery.Create(secondRouteId, Guid.NewGuid(), 1);
        var ignored = Delivery.Create(ignoredRouteId, Guid.NewGuid(), 1);
        await sut.AddRangeAsync([first, second, ignored], default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetByRouteIdsAsync([firstRouteId, secondRouteId], default);

        result.Should().BeEquivalentTo([first, second]);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-delivery-repository-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
