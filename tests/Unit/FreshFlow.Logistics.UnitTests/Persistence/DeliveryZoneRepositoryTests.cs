using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryZoneRepositoryTests
{
    [Fact]
    public async Task AddAndFindByIdAsync_ReturnsPersistedZone()
    {
        using var db = CreateContext();
        var sut = new DeliveryZoneRepository(db);
        var zone = new DeliveryZone("district_1", "District 1", null);

        await sut.AddAsync(zone, default);
        await sut.SaveChangesAsync(default);

        var found = await sut.FindByIdAsync(zone.Id, default);
        found.Should().NotBeNull();
        found!.Code.Should().Be("DISTRICT_1");
    }

    [Fact]
    public async Task CodeExistsAsync_NormalizesCodeAndIgnoresSoftDeletedZones()
    {
        using var db = CreateContext();
        var sut = new DeliveryZoneRepository(db);
        var active = new DeliveryZone("district_1", "District 1", null);
        var deleted = new DeliveryZone("district_2", "District 2", null);
        deleted.Deactivate();
        await sut.AddAsync(active, default);
        await sut.AddAsync(deleted, default);
        await sut.SaveChangesAsync(default);

        (await sut.CodeExistsAsync("DISTRICT_1", default)).Should().BeTrue();
        (await sut.CodeExistsAsync("district_1", default)).Should().BeTrue();
        (await sut.CodeExistsAsync("DISTRICT_2", default)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ActiveOnlyTrue_ReturnsOnlyActiveNotDeletedZonesOrderedByCode()
    {
        using var db = CreateContext();
        var sut = new DeliveryZoneRepository(db);
        var district2 = new DeliveryZone("DISTRICT_2", "District 2", null);
        var district1 = new DeliveryZone("DISTRICT_1", "District 1", null);
        var deleted = new DeliveryZone("DISTRICT_3", "District 3", null);
        deleted.Deactivate();
        await sut.AddAsync(district2, default);
        await sut.AddAsync(district1, default);
        await sut.AddAsync(deleted, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetAllAsync(activeOnly: true, default);

        result.Select(z => z.Code).Should().Equal("DISTRICT_1", "DISTRICT_2");
    }

    [Fact]
    public async Task GetAllAsync_ActiveOnlyFalse_ReturnsAllZones()
    {
        using var db = CreateContext();
        var sut = new DeliveryZoneRepository(db);
        var active = new DeliveryZone("DISTRICT_1", "District 1", null);
        var deleted = new DeliveryZone("DISTRICT_2", "District 2", null);
        deleted.Deactivate();
        await sut.AddAsync(active, default);
        await sut.AddAsync(deleted, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetAllAsync(activeOnly: false, default);

        result.Should().HaveCount(2);
        result.Should().Contain(z => z.DeletedAt != null);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-zone-repo-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
