using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class VehiclePersistenceConfigurationTests
{
    [Fact]
    public void Model_RegistersVehicleEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(Vehicle));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("vehicles");
    }

    [Fact]
    public void VehicleConfiguration_UsesSnakeCaseColumnsAndIndexes()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(Vehicle))!;
        var table = StoreObjectIdentifier.Table("vehicles", null);

        entity.FindProperty(nameof(Vehicle.PlateNumber))!
            .GetColumnName(table)
            .Should().Be("plate_number");
        entity.FindProperty(nameof(Vehicle.CapacityKg))!
            .GetColumnName(table)
            .Should().Be("capacity_kg");
        entity.FindProperty(nameof(Vehicle.VehicleType))!
            .GetColumnName(table)
            .Should().Be("vehicle_type");
        entity.FindProperty(nameof(Vehicle.IsAvailable))!
            .GetColumnName(table)
            .Should().Be("is_available");
        entity.FindProperty(nameof(Vehicle.RegisteredBy))!
            .GetColumnName(table)
            .Should().Be("registered_by");
        entity.FindProperty(nameof(Vehicle.DeletedAt))!
            .GetColumnName(table)
            .Should().Be("deleted_at");

        entity.GetForeignKeys().Should().BeEmpty(
            "vehicles.registered_by is a cross-module plain Guid");

        var plateIndex = entity.GetIndexes()
            .Single(i => i.GetDatabaseName() == "ux_vehicles_plate_number_active");
        plateIndex.IsUnique.Should().BeTrue();
        plateIndex.GetFilter().Should().Be("deleted_at IS NULL");

        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_vehicles_deleted_at");
    }

    [Fact]
    public void AddLogisticsModule_RegistersLogisticsRepositoriesAndReaders()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"logistics-di-{Guid.NewGuid()}"));

        services.AddLogisticsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IVehicleRepository>().Should().NotBeNull();
        provider.GetRequiredService<IDriverReader>().Should().NotBeNull();
        provider.GetRequiredService<IVehicleCapacityPolicy>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
