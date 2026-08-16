using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Queries.ListVehicles;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListVehiclesQueryHandlerTests
{
    [Fact]
    public async Task Query_DefaultPageSize_Is50()
    {
        var query = new ListVehiclesQuery();

        query.PageSize.Should().Be(50);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_WithCursor_ReturnsNextPageWithoutDuplicatesAsync()
    {
        using var db = CreateContext();
        var repository = new VehicleRepository(db);
        var sut = new ListVehiclesQueryHandler(repository);
        var baseTime = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddVehicleAsync(repository, db, "NEW-001", baseTime.AddSeconds(3));
        var middle = await AddVehicleAsync(repository, db, "MID-001", baseTime.AddSeconds(2));
        var oldest = await AddVehicleAsync(repository, db, "OLD-001", baseTime.AddSeconds(1));

        var firstPage = await sut.Handle(new ListVehiclesQuery(PageSize: 1), default);
        var secondPage = await sut.Handle(new ListVehiclesQuery(firstPage.Value.NextCursor, 1), default);
        var thirdPage = await sut.Handle(new ListVehiclesQuery(secondPage.Value.NextCursor, 1), default);

        firstPage.Value.Items.Should().ContainSingle(v => v.Id == newest.Id);
        firstPage.Value.NextCursor.Should().NotBeNull();
        secondPage.Value.Items.Should().ContainSingle(v => v.Id == middle.Id);
        secondPage.Value.Items.Single().Id.Should().NotBe(firstPage.Value.Items.Single().Id);
        secondPage.Value.NextCursor.Should().NotBeNull();
        thirdPage.Value.Items.Should().ContainSingle(v => v.Id == oldest.Id);
        thirdPage.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_IsActiveTrue_ReturnsOnlyActiveVehiclesAsync()
    {
        using var db = CreateContext();
        var repository = new VehicleRepository(db);
        var sut = new ListVehiclesQueryHandler(repository);
        var active = await AddVehicleAsync(repository, db, "ACT-001", DateTime.UtcNow.AddSeconds(2));
        var inactive = await AddVehicleAsync(repository, db, "DEL-001", DateTime.UtcNow.AddSeconds(1));
        inactive.Deactivate();
        await repository.SaveChangesAsync(default);

        var result = await sut.Handle(new ListVehiclesQuery(IsActive: true), default);

        result.Value.Items.Should().ContainSingle(v => v.Id == active.Id);
        result.Value.Items.Should().OnlyContain(v => v.IsActive);
    }

    [Fact]
    public async Task Handle_IsActiveFalse_ReturnsOnlyInactiveVehiclesAsync()
    {
        using var db = CreateContext();
        var repository = new VehicleRepository(db);
        var sut = new ListVehiclesQueryHandler(repository);
        await AddVehicleAsync(repository, db, "ACT-001", DateTime.UtcNow.AddSeconds(2));
        var inactive = await AddVehicleAsync(repository, db, "DEL-001", DateTime.UtcNow.AddSeconds(1));
        inactive.Deactivate();
        await repository.SaveChangesAsync(default);

        var result = await sut.Handle(new ListVehiclesQuery(IsActive: false), default);

        result.Value.Items.Should().ContainSingle(v => v.Id == inactive.Id);
        result.Value.Items.Should().OnlyContain(v => !v.IsActive);
    }

    [Fact]
    public async Task Handle_IsActiveNull_ReturnsActiveAndInactiveVehiclesAsync()
    {
        using var db = CreateContext();
        var repository = new VehicleRepository(db);
        var sut = new ListVehiclesQueryHandler(repository);
        await AddVehicleAsync(repository, db, "ACT-001", DateTime.UtcNow.AddSeconds(2));
        var inactive = await AddVehicleAsync(repository, db, "DEL-001", DateTime.UtcNow.AddSeconds(1));
        inactive.Deactivate();
        await repository.SaveChangesAsync(default);

        var result = await sut.Handle(new ListVehiclesQuery(IsActive: null), default);

        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(v => v.IsActive);
        result.Value.Items.Should().Contain(v => !v.IsActive);
    }

    [Fact]
    public async Task Handle_HubId_ReturnsOnlyVehiclesInHubAsync()
    {
        using var db = CreateContext();
        var repository = new VehicleRepository(db);
        var sut = new ListVehiclesQueryHandler(repository);
        var hubId = Guid.NewGuid();
        await AddVehicleAsync(repository, db, "HUB-001", DateTime.UtcNow, hubId);
        await AddVehicleAsync(repository, db, "OTHER-001", DateTime.UtcNow, Guid.NewGuid());
        await AddVehicleAsync(repository, db, "NONE-001", DateTime.UtcNow);

        var result = await sut.Handle(new ListVehiclesQuery(HubId: hubId), default);

        result.Value.Items.Should().ContainSingle(vehicle => vehicle.HubId == hubId);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-list-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Vehicle> AddVehicleAsync(
        VehicleRepository repository,
        AppDbContext db,
        string plateNumber,
        DateTime createdAt,
        Guid? hubId = null)
    {
        var vehicle = new Vehicle(plateNumber, 1200, VehicleType.van, null, hubId);
        await repository.AddAsync(vehicle, default);
        await repository.SaveChangesAsync(default);

        db.Entry(vehicle).Property(v => v.CreatedAt).CurrentValue = createdAt;
        db.Entry(vehicle).Property(v => v.UpdatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return vehicle;
    }
}
