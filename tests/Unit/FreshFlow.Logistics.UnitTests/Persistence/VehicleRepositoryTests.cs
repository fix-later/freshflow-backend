using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class VehicleRepositoryTests
{
    [Fact]
    public async Task AddAsync_AndFindByIdAsync_PersistAndReturnVehicleAsync()
    {
        using var db = CreateContext();
        var sut = new VehicleRepository(db);
        var registeredBy = Guid.NewGuid();
        var vehicle = new Vehicle("ABC-123", 1200, VehicleType.van, registeredBy);

        await sut.AddAsync(vehicle, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.FindByIdAsync(vehicle.Id, default);
        result.Should().NotBeNull();
        result!.PlateNumber.Should().Be("ABC-123");
        result.RegisteredBy.Should().Be(registeredBy);
    }

    [Fact]
    public async Task PlateNumberExistsAsync_OnlyChecksActiveVehiclesAndHonorsExcludeIdAsync()
    {
        using var db = CreateContext();
        var sut = new VehicleRepository(db);
        var active = new Vehicle("ABC-123", 1200, VehicleType.van, null);
        var inactive = new Vehicle("DEL-123", 1200, VehicleType.truck, null);
        inactive.Deactivate();
        await sut.AddAsync(active, default);
        await sut.AddAsync(inactive, default);
        await sut.SaveChangesAsync(default);

        var activeExists = await sut.PlateNumberExistsAsync(" ABC-123 ", null, default);
        var activeExcluded = await sut.PlateNumberExistsAsync("ABC-123", active.Id, default);
        var inactiveExists = await sut.PlateNumberExistsAsync("DEL-123", null, default);

        activeExists.Should().BeTrue();
        activeExcluded.Should().BeFalse();
        inactiveExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetPageAsync_MoreRowsThanPageSize_ReturnsNextCursorAsync()
    {
        using var db = CreateContext();
        var sut = new VehicleRepository(db);
        var baseTime = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddVehicleAsync(sut, db, "NEW-001", baseTime.AddSeconds(2));
        await AddVehicleAsync(sut, db, "OLD-001", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(null, 1, null, default);

        result.Items.Should().ContainSingle(v => v.Id == newest.Id);
        result.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_MalformedCursor_IgnoresCursorAsync()
    {
        using var db = CreateContext();
        var sut = new VehicleRepository(db);
        var newest = await AddVehicleAsync(sut, db, "NEW-001", DateTime.UtcNow.AddSeconds(2));
        await AddVehicleAsync(sut, db, "OLD-001", DateTime.UtcNow.AddSeconds(1));

        var result = await sut.GetPageAsync("not-valid-base64", 1, null, default);

        result.Items.Should().ContainSingle(v => v.Id == newest.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageAsync_InvalidPageSize_ThrowsArgumentExceptionAsync(int pageSize)
    {
        using var db = CreateContext();
        var sut = new VehicleRepository(db);
        Func<Task> act = () => sut.GetPageAsync(null, pageSize, null, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-repository-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Vehicle> AddVehicleAsync(
        VehicleRepository repository,
        AppDbContext db,
        string plateNumber,
        DateTime createdAt)
    {
        var vehicle = new Vehicle(plateNumber, 1200, VehicleType.van, null);
        await repository.AddAsync(vehicle, default);
        await repository.SaveChangesAsync(default);
        db.Entry(vehicle).Property(v => v.CreatedAt).CurrentValue = createdAt;
        db.Entry(vehicle).Property(v => v.UpdatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return vehicle;
    }
}
